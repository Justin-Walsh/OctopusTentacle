﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<V1PodList> ListAllPods(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task Create(V1Pod pod, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);

#pragma warning disable CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
        IAsyncEnumerable<string?> StreamPodLogs(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken = default);
#pragma warning restore CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        readonly ISystemLog log;

        public KubernetesPodService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider)
        {
            this.log = log;
        }

        public async Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken) =>
            await TryGetAsync(() => Client.ReadNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken));

        public async Task<V1PodList> ListAllPods(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedPodAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                resourceVersion: initialResourceVersion,
                watch: true,
                cancellationToken: cancellationToken);

            var watchErrorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Action<Exception> internalOnError = ex =>
            {
                //We cancel the watch explicitly (so it can be restarted)
                watchErrorCancellationTokenSource.Cancel();

                //notify there was an error
                onError(ex);
            };

            await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
            {
                await onChange(type, pod);
            }
        }

        public async IAsyncEnumerable<string?> StreamPodLogs(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var enumerable = StreamPodLogsViaPolling(podName, containerName, cancellationToken);
            //var enumerable = StreamPodLogsViaOpenStream(podName, containerName, cancellationToken);

            await foreach (var line in enumerable)
            {
                yield return line;
            }
        }

        async IAsyncEnumerable<string?> StreamPodLogsViaOpenStream(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Stream? logStream = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpOperationResponse<Stream> response;
                try
                {
                    response = await Client.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(
                            podName,
                            KubernetesConfig.Namespace,
                            containerName,
                            follow: true,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // A BadRequest probably means the pod is still starting, so lets silently delay and retry
                    await Task.Delay(250, cancellationToken);
                    continue;
                }
                catch (HttpOperationException ex)
                {
                    log.Warn(ex, $"Failed to read namespaced logs for pod {podName}. Response.: {ex.Response.Content}");
                    await Task.Delay(250, cancellationToken);
                    continue;
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                logStream = response.Body;
            }

            //if the log stream is null at this point, just jump out
            if (logStream is null)
                yield break;

            log.Verbose($"Reading open log stream for pod {podName}");
            using var streamReader = new StreamReader(logStream);
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await streamReader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

                    if (line is not null)
                    {
                        log.Verbose($"Read log line {line} for pod {podName}");
                    }
                }
                catch (TaskCanceledException)
                {
                    yield break;
                }

                yield return line;
            }
        }

        async IAsyncEnumerable<string?> StreamPodLogsViaPolling(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            DateTime? lastRetrievedTime = null;
            var hasReadEndOfScriptControlMessage = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var secondsSinceLastCheck = lastRetrievedTime.HasValue ? (int)Math.Floor((now - lastRetrievedTime.Value).TotalSeconds) : (int?)null;

                log.Verbose($"Getting logs for pod {podName}, seconds since last check {secondsSinceLastCheck}");

                Stream logStream;
                try
                {
                    logStream = await Client.ReadNamespacedPodLogAsync(podName,
                        KubernetesConfig.Namespace,
                        containerName,
                        sinceSeconds: secondsSinceLastCheck,
                        cancellationToken: cancellationToken);
                }
                catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.BadRequest)
                {
                    //a BadRequest probably means the pod is still starting, so lets silently delay and retry
                    await Task.Delay(250, cancellationToken);
                    continue;
                }
                catch (HttpOperationException ex)
                {
                    log.Warn(ex, $"Failed to read namespaced logs for pod {podName}. Response.: {ex.Response.Content}");
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                using var streamReader = new StreamReader(logStream);
                while (!streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    log.Verbose($"Reading log line for pod {podName}");
                    var line = await streamReader.ReadLineAsync().ConfigureAwait(false);
                    if (line is not null)
                    {
                        log.Verbose($"Read log line {line} for pod {podName}");
                    }

                    yield return line;

                    if (line is not null && line.Contains(KubernetesConfig.EndOfScriptControlMessage))
                    {
                        hasReadEndOfScriptControlMessage = true;
                        break;
                    }
                }

                //don't loop again
                if (hasReadEndOfScriptControlMessage)
                    break;

                //we add the number of seconds onto the last retrieved time, just in case it took us a while to read the previous logs from the stream
                lastRetrievedTime = (lastRetrievedTime ?? now).AddSeconds(secondsSinceLastCheck.GetValueOrDefault(0));

                //delay for 1 second
                await Task.Delay(1000, cancellationToken);
            }
        }

        public async Task Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            await Client.CreateNamespacedPodAsync(pod, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}
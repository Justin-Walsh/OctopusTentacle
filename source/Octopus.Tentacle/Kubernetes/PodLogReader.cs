using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;

namespace Octopus.Tentacle.Kubernetes
{
    public class PodLogReader
    {
        public k8s.Kubernetes Client { get; }

        public PodLogReader(IKubernetesClientConfigProvider configProvider)
        {
            Client = new k8s.Kubernetes(configProvider.Get());
        }

        // public void Append(string key, params object[] values)
        // {
        //     foreach (var value in values)
        //     {
        //         switch (value)
        //         {
        //             case int intval:
        //                 parameters.Add($"{key}={intval}");
        //                 break;
        //             case string strval:
        //                 parameters.Add($"{key}={Uri.EscapeDataString(strval)}");
        //                 break;
        //             case bool boolval:
        //                 parameters.Add($"{key}={(boolval ? "true" : "false")}");
        //                 break;
        //             default:
        //                 // null
        //                 break;
        //         }
        //     }
        // }
        public async Task<Stream> ReadPodLogsSince(string podName, string containerName, CancellationToken ct, string sinceTime)
        {
            var url = $"api/v1/namespaces/{KubernetesConfig.Namespace}/pods/{podName}/log";
            // var q = new AbstractKubernetes.QueryBuilder();
            // q.Append("container", container);
            // q.Append("follow", follow);
            // q.Append("insecureSkipTLSVerifyBackend", insecureSkipTLSVerifyBackend);
            // q.Append("limitBytes", limitBytes);
            // q.Append("pretty", pretty);
            // q.Append("previous", previous);
            // q.Append("sinceSeconds", sinceSeconds);
            // q.Append("tailLines", tailLines);
            // q.Append("timestamps", timestamps);
            // url += q.ToString();

            url += $"?container={containerName}&sinceTime={Uri.EscapeDataString(sinceTime)}";
            // we need to get the base uri, as it's not set on the HttpClient
            url = string.Concat( Client.BaseUri, url );

            var httpRequest = new HttpRequestMessage( HttpMethod.Get, url );

            if ( Client.Credentials != null )
            {
                await Client.Credentials.ProcessHttpRequestAsync( httpRequest, CancellationToken.None );
            }

            var response = await Client.HttpClient.SendAsync( httpRequest, HttpCompletionOption.ResponseHeadersRead );

            return await response.Content.ReadAsStreamAsync();
            // return await ReadNamespacedPodLogAsync(Client, podName,
            //     KubernetesConfig.Namespace,
            //     containerName,
            //     sinceSeconds: secondsSince,
            //     cancellationToken: ct);
        }
    }
}
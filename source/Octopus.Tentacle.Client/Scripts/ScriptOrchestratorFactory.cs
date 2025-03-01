﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptOrchestratorFactory : IScriptOrchestratorFactory
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha;
        readonly IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;
        readonly TentacleClientOptions clientOptions;

        public ScriptOrchestratorFactory(
            IAsyncClientScriptService clientScriptServiceV1,
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha,
            IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientScriptServiceV3Alpha = clientScriptServiceV3Alpha;
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public async Task<IScriptOrchestrator> CreateOrchestrator(CancellationToken cancellationToken)
        {
            ScriptServiceVersion scriptServiceToUse;
            try
            {
                scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }

            return scriptServiceToUse switch
            {
                ScriptServiceVersion.Version1 => new ScriptServiceV1Orchestrator(
                    clientScriptServiceV1,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    clientOptions,
                    logger),

                ScriptServiceVersion.Version2 => new ScriptServiceV2Orchestrator(
                    clientScriptServiceV2,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger),

                ScriptServiceVersion.Version3Alpha => new ScriptServiceV3AlphaOrchestrator(
                    clientScriptServiceV3Alpha,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger),

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
        {
            logger.Verbose("Determining ScriptService version to use");

            async Task<CapabilitiesResponseV2> GetCapabilitiesFunc(CancellationToken ct)
            {
                var result = await clientCapabilitiesServiceV2.GetCapabilitiesAsync(new HalibutProxyRequestOptions(ct));

                return result;
            }

            var tentacleCapabilities = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                GetCapabilitiesFunc,
                logger,
                clientOperationMetricsBuilder,
                cancellationToken);

            logger.Verbose($"Discovered Tentacle capabilities: {string.Join(",", tentacleCapabilities.SupportedCapabilities)}");

            if (tentacleCapabilities.HasScriptServiceV3Alpha())
            {
                //if the service is not disabled, we can use it :)
                if (!clientOptions.DisableScriptServiceV3Alpha)
                {
                    logger.Verbose("Using ScriptServiceV3Alpha");
                    logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                        ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                        : "RPC call retries are disabled.");
                    return ScriptServiceVersion.Version3Alpha;
                }

                logger.Verbose("ScriptServiceV3Alpha is disabled and will not be used.");
            }

            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                    ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                    : "RPC call retries are disabled.");
                return ScriptServiceVersion.Version2;
            }

            logger.Verbose("RPC call retries are enabled but will not be used for Script Execution as a compatible ScriptService was not found. Please upgrade Tentacle to enable this feature.");
            logger.Verbose("Using ScriptServiceV1");
            return ScriptServiceVersion.Version1;
        }
    }
}
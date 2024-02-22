using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

        public static string NamespaceVariableName => $"{EnvVarPrefix}__NAMESPACE";
        public static string Namespace => GetRequiredEnvVar(NamespaceVariableName, "Unable to determine Kubernetes namespace.");
        public static string JobServiceAccountName => GetRequiredEnvVar($"{EnvVarPrefix}__PODSERVICEACCOUNTNAME", "Unable to determine Kubernetes Pod service account name.");
        public static string PodVolumeJson => GetRequiredEnvVar($"{EnvVarPrefix}__PODVOLUMEJSON", "Unable to determine Kubernetes Pod volume yaml.");
        public static bool UsePods => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__USEPODS"), out var usePods) && usePods;
        public static string HelmReleaseNameVariableName => $"{EnvVarPrefix}__HELMRELEASENAME";
        public static string HelmReleaseName => GetRequiredEnvVar(HelmReleaseNameVariableName, "Unable to determine Helm release name.");

        public static string HelmChartVersionVariableName => $"{EnvVarPrefix}__HELMCHARTVERSION";
        public static string HelmChartVersion => GetRequiredEnvVar(HelmChartVersionVariableName, "Unable to determine Helm chart version.");

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class KubernetesScriptExecutionContext : IScriptExecutionContext
    {
        [JsonConstructor]
        public KubernetesScriptExecutionContext(string image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public KubernetesScriptExecutionContext()
        {
        }

        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}
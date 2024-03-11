using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using k8s;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class KubernetesLogFixture 
    {
       

        [Test]
        public async Task GetLogsSinceTime()
        {
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE", "octopus-agent-aksadmin");
            var sut = new PodLogReader(new LocalMachineKubernetesClientConfigProvider());

            var nodes = sut.Client.ListNode();
            nodes.Items.Should().NotBeEmpty();

            var logStream = await sut.ReadPodLogsSince("octopus-agent-tentacle-57d4c768dc-4lll9", "octopus-agent-tentacle", CancellationToken.None,
                "2024-03-11T00:58:24Z");
            
            using var streamReader = new StreamReader(logStream);
            var foo = await streamReader.ReadToEndAsync();
        }
    }
}
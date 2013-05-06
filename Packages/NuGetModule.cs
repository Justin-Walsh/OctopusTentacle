using System;
using Autofac;
using NuGet;
using Octopus.Shared.Configuration;
using log4net;

namespace Octopus.Shared.Packages
{
    public class NuGetModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            HttpClient.DefaultCredentialProvider = FeedCredentialsProvider.Instance;

            builder.Register(c =>
            {
                MachineCache.Default.Clear();
                return new OctopusPackageRepositoryFactory(c.Resolve<ILog>());
            }).As<IPackageRepositoryFactory>();
        }
    }
}
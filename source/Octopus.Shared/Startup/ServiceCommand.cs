using System;
using System.Collections.Generic;
using System.Reflection;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class ServiceCommand : AbstractCommand
    {
        readonly string serviceDescription;
        readonly Assembly assemblyContainingService;
        readonly ApplicationName applicationName;
        readonly IApplicationInstanceLocator instanceLocator;
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ServiceConfigurationState serviceConfigurationState;
        readonly IServiceConfigurator serviceConfigurator;
        readonly string ServicePasswordEnvVar = "OCTOPUS_SERVICE_PASSWORD";
        readonly string ServiceUsernameEnvVar = "OCTOPUS_SERVICE_USERNAME";

        string? instanceName;

        public ServiceCommand(ApplicationName applicationName,
            IApplicationInstanceLocator instanceLocator,
            IApplicationInstanceSelector instanceSelector,
            string serviceDescription,
            Assembly assemblyContainingService,
            IServiceConfigurator serviceConfigurator)
        {
            this.applicationName = applicationName;
            this.instanceLocator = instanceLocator;
            this.instanceSelector = instanceSelector;
            this.serviceDescription = serviceDescription;
            this.assemblyContainingService = assemblyContainingService;
            this.serviceConfigurator = serviceConfigurator;

            serviceConfigurationState = new ServiceConfigurationState
            {
                Username = Environment.GetEnvironmentVariable(ServiceUsernameEnvVar),
                Password = Environment.GetEnvironmentVariable(ServicePasswordEnvVar)
            };

            var serviceType = PlatformDetection.IsRunningOnWindows
                ? "Windows Service"
                : "system service";

            Options.Add("start", $"Start the {serviceType} if it is not already running", v => serviceConfigurationState.Start = true);
            Options.Add("stop", $"Stop the {serviceType} if it is running", v => serviceConfigurationState.Stop = true);
            Options.Add("restart", $"Restart the {serviceType} if it is running", v => serviceConfigurationState.Restart = true);
            Options.Add("reconfigure", $"Reconfigure the {serviceType}", v => serviceConfigurationState.Reconfigure = true);
            Options.Add("install", $"Install the {serviceType}", v => serviceConfigurationState.Install = true);
            Options.Add("username=|user=", $"Username to run the service under (DOMAIN\\Username format). Only used when --install or --reconfigure are used.  Can also be passed via an environment variable {ServiceUsernameEnvVar}.", v => serviceConfigurationState.Username = v);
            Options.Add("uninstall", $"Uninstall the {serviceType}", v => serviceConfigurationState.Uninstall = true);
            Options.Add("password=", $"Password for the username specified with --username. Only used when --install or --reconfigure are used. Can also be passed via an environment variable {ServicePasswordEnvVar}.", v =>
            {
                serviceConfigurationState.Password = v;
            }, sensitive: true);
            Options.Add("dependOn=", "", v => serviceConfigurationState.DependOn = v);
            Options.Add("instance=", "Name of the instance to use, or * to use all instances", v => instanceName = v);
        }

        protected override void Start()
        {
            var exePath = assemblyContainingService.FullProcessPath();

            if (instanceName == "*")
            {
                if (serviceConfigurationState.Reconfigure || serviceConfigurationState.Install || serviceConfigurationState.Uninstall)
                {
                    throw new ControlledFailureException("--instance=* can only be used for --start, --stop, and --restart flags");
                }

                var exceptions = new List<Exception>();

                foreach (var instance in instanceLocator.ListInstances())
                {
                    try
                    {
                        var thisServiceName = ServiceName.GetWindowsServiceName(applicationName, instance.InstanceName);
                        serviceConfigurator.ConfigureService(thisServiceName, exePath, instance.InstanceName, serviceDescription, serviceConfigurationState);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
            else
            {
                var currentName = instanceSelector.GetCurrentName();
                if (currentName == null)
                    throw new ArgumentException("Unable to locate instance configuration");
                var thisServiceName = ServiceName.GetWindowsServiceName(applicationName, currentName);
                serviceConfigurator.ConfigureService(thisServiceName, exePath, currentName, serviceDescription, serviceConfigurationState);
            }
        }
    }
}

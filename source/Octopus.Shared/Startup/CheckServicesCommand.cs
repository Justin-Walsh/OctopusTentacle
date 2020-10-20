﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Startup
{
    public class CheckServicesCommand : AbstractCommand
    {
        readonly ILog log;
        HashSet<string>? instances;
        readonly IApplicationInstanceLocator instanceLocator;
        readonly ApplicationName applicationName;

        public CheckServicesCommand(ILog log,
            IApplicationInstanceLocator instanceLocator,
            ApplicationName applicationName)
        {
            this.log = log;
            this.instanceLocator = instanceLocator;
            this.applicationName = applicationName;

            Options.Add("instances=", "Comma-separated list of instances to check, or * to check all instances", v =>
            {
                instances = new HashSet<string>(v.Split(',', ';'));
            });
        }

        protected override void Start()
        {
            if (instances == null)
                throw new ControlledFailureException("Use --instances argument to specify which instances to check. Use --instances=* to check all instances.");

            var startAll = instances.Count == 1 && instances.First() == "*";
            var serviceControllers = ServiceController.GetServices();
            try
            {
                foreach (var instance in instanceLocator.ListInstances())
                {
                    if (!startAll && instances.Contains(instance.InstanceName) == false)
                        continue;

                    var serviceName = ServiceName.GetWindowsServiceName(applicationName, instance.InstanceName);

                    var controller = serviceControllers.FirstOrDefault(s => s.ServiceName == serviceName);

                    if (controller != null &&
                        controller.Status != ServiceControllerStatus.Running &&
                        controller.Status != ServiceControllerStatus.StartPending)
                    {
                        try
                        {
                            controller.Start();
                            log.Info($"Service {serviceName} starting");

                            var waitUntil = DateTime.Now.AddSeconds(30);
                            while (controller.Status != ServiceControllerStatus.Running && DateTime.Now < waitUntil)
                            {
                                controller.Refresh();

                                log.Info("Waiting for service to start. Current status: " + controller.Status);
                                Thread.Sleep(300);
                            }

                            if (controller.Status == ServiceControllerStatus.Running)
                                log.Info($"Service {serviceName} started");
                            else
                                log.Info($"Service {serviceName} doesn't have Running status after 30sec. Status will be assessed again at the time of the next scheduled check.");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Service {serviceName} could not be started - {ex}");
                        }
                    }
                }
            }
            finally
            {
                foreach (var controller in serviceControllers)
                    controller.Dispose();
            }
        }
    }
}

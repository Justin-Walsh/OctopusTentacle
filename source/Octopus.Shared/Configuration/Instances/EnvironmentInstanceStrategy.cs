﻿using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Configuration.Instances
{
    public class EnvironmentInstanceStrategy : IApplicationInstanceStrategy
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IMapEnvironmentVariablesToConfigItems mapper;
        readonly IEnvironmentVariableReader reader;
        bool loaded;

        public EnvironmentInstanceStrategy(StartUpInstanceRequest startUpInstanceRequest, IMapEnvironmentVariablesToConfigItems mapper, IEnvironmentVariableReader reader)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.mapper = mapper;
            this.reader = reader;
        }

        public int Priority => 200;

        public bool AnyInstancesConfigured()
        {
            if (!(startUpInstanceRequest is StartUpDynamicInstanceRequest))
                return false;
            EnsureLoaded();
            return mapper.ConfigState == ConfigState.Complete;
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            if (!AnyInstancesConfigured())
                return Enumerable.Empty<ApplicationInstanceRecord>().ToList();
            return new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Environmental", true)
            };
        }

        public ILoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            EnsureLoaded();
            return new LoadedApplicationInstance(applicationInstance.InstanceName, new InMemoryKeyValueStore(mapper));
        }
                
        void EnsureLoaded()
        {
            if (!loaded)
            {
                var results = LoadFromEnvironment(reader, mapper);
                if (results.Values.Any(x => x != null))
                {
                    mapper.SetEnvironmentValues(results);
                }
            }
            loaded = true;
        }

        internal static Dictionary<string, string?> LoadFromEnvironment(IEnvironmentVariableReader reader, IMapEnvironmentVariablesToConfigItems mapper)
        {
            var results = new Dictionary<string, string?>();
            foreach (var variableName in mapper.SupportedEnvironmentVariables)
            {
                results.Add(variableName, reader.Get(variableName));
            }

            return results;
        }
    }
}
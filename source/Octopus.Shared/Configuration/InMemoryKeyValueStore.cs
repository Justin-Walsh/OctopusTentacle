﻿using System;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    public class InMemoryKeyValueStore : IAggregatableKeyValueStore
    {
        readonly IMapEnvironmentValuesToConfigItems mapper;

        public InMemoryKeyValueStore(IMapEnvironmentValuesToConfigItems mapper)
        {
            this.mapper = mapper;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return mapper.GetConfigurationValue(name);
        }

        public (bool foundResult, TData value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            object? data = mapper.GetConfigurationValue(name);

            if (data == null)
                return (false, default(TData)!);
            if (typeof(TData) == typeof(string))
                return (true, (TData) data);
            if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                return (true, (TData) (object) bool.Parse((string) data));
            if (typeof(TData).IsEnum)
                return (true, (TData) Enum.Parse(typeof(TData), ((string) data).Trim('"')));

            // See FlatDictionaryKeyValueStore.ValueNeedsToBeSerialized, some of the types are serialized, and will therefore expect to be
            // double quote delimited
            var dataType = typeof(TData);
            if (protectionLevel == ProtectionLevel.MachineKey || dataType.IsClass)
                return (true, JsonConvert.DeserializeObject<TData>("\"" + data + "\""));

            return (true, JsonConvert.DeserializeObject<TData>((string)data));
        }

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return false;
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return false;
        }

        public bool Remove(string name)
        {
            return false;
        }

        public bool Save()
        {
            return false;
        }
    }
}
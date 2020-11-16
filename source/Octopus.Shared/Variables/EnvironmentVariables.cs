﻿using System;
using System.Linq;

namespace Octopus.Shared.Variables
{
    public static class EnvironmentVariables
    {
        public const string TentacleProxyUsername = "TentacleProxyUsername";
        public const string TentacleProxyPassword = "TentacleProxyPassword";
        public const string TentacleProxyHost = "TentacleProxyHost";
        public const string TentacleProxyPort = "TentacleProxyPort";
        public const string TentacleUseDefaultProxy = "TentacleUseDefaultProxy";
        public const string TentacleVersion = "TentacleVersion";
        public const string TentacleCertificateSignatureAlgorithm = "TentacleCertificateSignatureAlgorithm";
        public const string TentacleHome = "TentacleHome";
        public const string TentacleApplications = "TentacleApplications";
        public const string TentacleJournal = "TentacleJournal";
        public const string TentacleInstanceName = "TentacleInstanceName";
        public const string TentacleExecutablePath = "TentacleExecutablePath";
        public const string TentacleProgramDirectoryPath = "TentacleProgramDirectoryPath";
        public const string AgentProgramDirectoryPath = "AgentProgramDirectoryPath";

        public static readonly string[] AllWellKnownEnvironmentVariables
            = typeof(EnvironmentVariables).GetFields().Select(f => (string)f.GetValue(null)!).ToArray();
    }
}
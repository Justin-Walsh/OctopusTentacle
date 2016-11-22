﻿using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Octopus.Shared.Util
{
    public static class EnvironmentHelper
    {
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = new List<string>();
            SafelyAddEnvironmentVarsToList(ref envVars);
            SafelyAddPathVarsToList(ref envVars);
            SafelyAddProcessVarsToList(ref envVars);
            SafelyAddComputerInfoVarsToList(ref envVars);
            return envVars.ToArray();
        }

        static void SafelyAddEnvironmentVarsToList(ref List<string> envVars)
        {
            try
            {
                envVars.Add($"OperatingSystem: {Environment.OSVersion.ToString()}");
                envVars.Add($"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
                envVars.Add($"MachineName: {Environment.MachineName}");
                envVars.Add($"CurrentUser: {Environment.UserName}");
                envVars.Add($"Is64BitProcess: {Environment.Is64BitProcess.ToString()}");
                envVars.Add($"ProcessorCount: {Environment.ProcessorCount.ToString()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddPathVarsToList(ref List<string> envVars)
        {
            try
            {
                envVars.Add($"TempDirectory: {Path.GetTempPath()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddProcessVarsToList(ref List<string> envVars)
        {
            try
            {
                envVars.Add($"HostProcessName: {Process.GetCurrentProcess().ToString()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddComputerInfoVarsToList(ref List<string> envVars)
        {
            try
            {
                var computerInfo = new ComputerInfo();
                envVars.Add($"TotalPhysicalMemory: {computerInfo.TotalPhysicalMemory.ToFileSizeString()}");
                envVars.Add($"AvailablePhysicalMemory: {computerInfo.AvailablePhysicalMemory.ToFileSizeString()}");
            }
            catch
            {
                // silently fail.
            }
        }
    }
}

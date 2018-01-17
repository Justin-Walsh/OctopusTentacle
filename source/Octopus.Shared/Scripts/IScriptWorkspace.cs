using System;
using System.Collections.Generic;
using System.Net;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Scripts
{
    public interface IScriptWorkspace
    {
        string WorkingDirectory { get; }
        string BootstrapScriptFilePath { get; }
        string[] ScriptArguments { get; set; }
        NetworkCredential RunAs { get; set; }
        IDictionary<string, string> CustomEnvironmentVariables { get; set; }
        ScriptIsolationLevel IsolationLevel { get; set; }
        TimeSpan ScriptMutexAcquireTimeout { get; set; }
        void BootstrapScript(string scriptBody);
        string ResolvePath(string fileName);
        void Delete();
    }
}
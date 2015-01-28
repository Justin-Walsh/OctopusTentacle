using System;

namespace Octopus.Platform.Util
{
    public interface IFileInfo
    {
        string FullPath { get; }
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
    }
}
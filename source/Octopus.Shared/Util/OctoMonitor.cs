﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Octopus.Diagnostics;

namespace Octopus.Shared.Util
{
    public class OctoMonitor
    {
        public static readonly TimeSpan DefaultInitialAcquisitionAttemptTimeout = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan DefaultWaitBetweenAcquisitionAttempts = TimeSpan.FromSeconds(10);

        public static TimeSpan InitialAcquisitionAttemptTimeout = DefaultInitialAcquisitionAttemptTimeout;
        public static TimeSpan WaitBetweenAcquisitionAttempts = DefaultWaitBetweenAcquisitionAttempts;

        public static ILog SystemLog = Diagnostics.Log.System();

        public static IDisposable Enter(object obj, CancellationToken cancellationToken, ILog log)
            => Enter(obj, null, cancellationToken, log);

        public static IDisposable Enter(object obj, string? waitMessage, CancellationToken cancellationToken, ILog log)
        {
            SystemLog.Trace($"Acquiring monitor {obj}");
            cancellationToken.ThrowIfCancellationRequested();

            // Try to acquire the monitor lock for a few seconds before reporting we are going to start waiting
            if (TryAcquire(obj, InitialAcquisitionAttemptTimeout, out var mutexReleaser))
            {
                SystemLog.Trace($"Acquired monitor {obj}");
                return new OctoMonitorReleaser(obj);
            }

            // Go into an acquisition loop supporting cooperative cancellation
            cancellationToken.ThrowIfCancellationRequested();
            LogWaiting(obj, waitMessage, log);
            while (true)
            {
                if (TryAcquire(obj, WaitBetweenAcquisitionAttempts, out mutexReleaser))
                    return mutexReleaser;

                cancellationToken.ThrowIfCancellationRequested();
                LogWaiting(obj, waitMessage, log);
            }
        }

        static bool TryAcquire(object obj,
            TimeSpan timeout,
            [NotNullWhen(true)]
            out IDisposable? mutexReleaser)
        {
            mutexReleaser = null;
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(obj, timeout, ref lockTaken);
                if (lockTaken)
                {
                    SystemLog.Trace($"Acquired monitor {obj}");
                    mutexReleaser = new OctoMonitorReleaser(obj);
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (lockTaken) Monitor.Exit(obj);
                SystemLog.Warn(ex, $"Exception thrown while entering monitor {obj}");
            }

            return false;
        }

        static void LogWaiting(object obj, string? waitMessage, ILog log)
        {
            SystemLog.Verbose($"Monitor {obj} in use, waiting. {waitMessage}");
            if (!string.IsNullOrWhiteSpace(waitMessage))
                log.Info(waitMessage);
        }

        class OctoMonitorReleaser : IDisposable
        {
            readonly object obj;

            public OctoMonitorReleaser(object obj)
            {
                this.obj = obj;
            }

            public void Dispose()
            {
                try
                {
                    Monitor.Exit(obj);
                }
                catch (Exception ex)
                {
                    Diagnostics.Log.System().Warn(ex, $"Exception thrown while releasing monitor {obj}");
                }

                Diagnostics.Log.System().Trace($"Released monitor {obj}");
            }
        }
    }
}
// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// "An Agent is being started as administrator right now" (beta.10, the Agent's admin mode, owner decision D34): a named
/// mutex beside the data folder's <see cref="AgentInstanceLock"/>, held by whoever shows the UAC prompt — the App starting
/// the Agent, or an Agent the logon task started — until the elevated Agent has claimed the folder or has exited.
/// </summary>
/// <remarks>
/// The App's launcher and a starting Agent both read it as "an Agent is coming": without it, the App at logon would find no
/// Agent while the task's prompt is still on the taskbar, start its own, and prompt a second time — two prompts, and two
/// Agents if both were accepted. Held, never waited on, released with its handle, exactly as the instance lock is.
/// </remarks>
public sealed class ElevationMarker : IDisposable
{
    private readonly Mutex _mutex;

    private ElevationMarker(Mutex mutex) => _mutex = mutex;

    /// <summary><c>Local\FrameLedger.Agent.Elevating.&lt;32 hex digits&gt;</c> — the instance lock's key, its own name.</summary>
    public static string NameFor(string dataDirectory) =>
        AgentInstanceLock.NameFor(dataDirectory).Replace(@"Local\FrameLedger.Agent.", @"Local\FrameLedger.Agent.Elevating.", StringComparison.Ordinal);

    /// <summary>The marker for <paramref name="dataDirectory"/>, or null when another process already holds it.</summary>
    public static ElevationMarker? TryAcquire(string dataDirectory)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, NameFor(dataDirectory), out bool createdNew);
            if (!createdNew)
            {
                return null;
            }

            var marker = new ElevationMarker(mutex);
            mutex = null;
            return marker;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            return null;
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    /// <summary>True while any process, this one included, holds the marker for <paramref name="dataDirectory"/>.</summary>
    public static bool IsHeld(string dataDirectory)
    {
        try
        {
            if (!Mutex.TryOpenExisting(NameFor(dataDirectory), out Mutex? existing))
            {
                return false;
            }

            existing.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public void Dispose() => _mutex.Dispose();
}

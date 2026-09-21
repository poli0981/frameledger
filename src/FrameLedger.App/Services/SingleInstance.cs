using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FrameLedger.App.Services;

/// <summary>
/// One App per data folder per Windows session (2026-09-22). Until this existed nothing stopped a second App: the
/// owner's log of 2026-09-21 shows two started 360 ms apart from one double-click — two shells, two tray icons, a
/// second log file (<c>ui-…_001.log</c>, because the first held the day's), both writing one ledger — and with
/// "minimize to tray" on, every click on the shortcut of an App that was already running made another.
/// </summary>
/// <remarks>
/// <para>
/// The first instance owns a named mutex and listens on a named event; a later one finds the mutex taken, sets the
/// event and exits, and the first brings its window forward (<see cref="ShellHost.Reveal"/>). Both names carry a hash
/// of the data directory, so an App pointed at another folder is its own instance — the same key the Agent's
/// one-per-data-folder rule uses (#201).
/// </para>
/// <para>
/// <c>Local\</c> on purpose: another Windows session is another desktop, and its user cannot be shown this window.
/// An instance started as administrator creates objects a standard-user instance may not open; that is still "an
/// instance is running", so every failure to open is answered as <see cref="Claim.AnotherInstanceIsRunning"/> —
/// the second App never starts just because it could not say so.
/// </para>
/// <para>
/// <c>--diag</c> never claims: it opens no window and must work beside a running App (<c>10_LOGGING</c>). Velopack's
/// lifecycle hooks run before this and exit the process on their own.
/// </para>
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _reveal;
    private readonly RegisteredWaitHandle _registration;
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle reveal)
    {
        _mutex = mutex;
        _reveal = reveal;
        _registration = ThreadPool.RegisterWaitForSingleObject(reveal, (_, timedOut) =>
        {
            if (!timedOut)
            {
                RevealRequested?.Invoke(this, EventArgs.Empty);
            }
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Raised on a thread-pool thread when another instance was started and handed over; the App marshals it.</summary>
    public event EventHandler? RevealRequested;

    /// <summary>What a start found.</summary>
    public enum Claim
    {
        /// <summary>This is the instance; keep the returned object alive for the process's life.</summary>
        Acquired,

        /// <summary>Another instance owns the data folder; it was asked to show itself where that was possible.</summary>
        AnotherInstanceIsRunning,
    }

    /// <summary>The object names for a data directory: a hash, because a path is not a legal kernel object name.</summary>
    public static string KeyOf(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory.TrimEnd('\\', '/').ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 8);
    }

    /// <summary>Claims the data folder, or tells the instance that already has it to show itself.</summary>
    public static Claim TryClaim(string key, out SingleInstance? instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        instance = null;
        string mutexName = @"Local\FrameLedger.App." + key;
        string eventName = @"Local\FrameLedger.App.Reveal." + key;

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                SignalExisting(eventName);
                return Claim.AnotherInstanceIsRunning;
            }

            var reveal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, eventName);
            instance = new SingleInstance(mutex, reveal);
            mutex = null;
            return Claim.Acquired;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
        {
            // The name exists and is not ours to open: an instance started as administrator. It is running.
            return Claim.AnotherInstanceIsRunning;
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _registration.Unregister(null);
        _reveal.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Disposed on a thread other than the one that claimed it: the handle closing below releases it all the same.
        }

        _mutex.Dispose();
    }

    private static void SignalExisting(string eventName)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(eventName, out EventWaitHandle? existing))
            {
                using (existing)
                {
                    _ = existing.Set();
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // It is running and cannot be told; this process still must not become a second App.
        }
    }
}

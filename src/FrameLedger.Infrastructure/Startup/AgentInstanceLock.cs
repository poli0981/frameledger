using System.Security.Cryptography;
using System.Text;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// One capturing Agent per data folder (2026-09-17): a named mutex in this logon session's namespace, keyed by the
/// folder, that <c>--serve</c> and the console's capture verbs claim before logging, rules or the ledger, and that the
/// App probes before it starts an Agent. Without it, 0.1.0-beta.1 ran four: an elevated App could not connect to the
/// Agent already serving (<c>07_IPC</c> §C), started another on every connect round, and every game launched in the next
/// hour was injected, read and recorded by all four.
/// </summary>
/// <remarks>
/// <para>
/// <b>Held, never waited on.</b> The kernel object lives exactly as long as some process holds a handle to it, so
/// "created, not opened" is the claim, and a crashed holder releases it with its handles: there is no abandoned state and
/// nothing to clean up.
/// </para>
/// <para>
/// <b>Across elevation.</b> An Agent started by an elevated App creates the object under an administrator's default
/// security, which an unelevated process may not open at all. <see cref="UnauthorizedAccessException"/> therefore means
/// "held by someone", never "free". The other direction opens normally.
/// </para>
/// <para>
/// <b>The name carries a hash of the folder, not the folder</b>: one key however the path is spelled, and no user path in
/// the object namespace. A test's temporary folder is a different key, so a test never collides with an installed Agent.
/// </para>
/// </remarks>
public sealed class AgentInstanceLock : IDisposable
{
    /// <summary>The exit code of an Agent refused because another process holds its data folder.</summary>
    public const int ExitHeldElsewhere = 10;

    private readonly Mutex _mutex;

    private AgentInstanceLock(Mutex mutex, string name)
    {
        _mutex = mutex;
        Name = name;
    }

    public string Name { get; }

    /// <summary><c>Local\FrameLedger.Agent.&lt;32 hex digits&gt;</c> for <paramref name="dataDirectory"/>, however it is spelled.</summary>
    public static string NameFor(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory)).ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(folder));
        return @"Local\FrameLedger.Agent." + Convert.ToHexString(hash, 0, 16);
    }

    /// <summary>The claim on <paramref name="dataDirectory"/>, or null when another process already holds it.</summary>
    public static AgentInstanceLock? TryAcquire(string dataDirectory)
    {
        string name = NameFor(dataDirectory);
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, name, out bool createdNew);
            if (!createdNew)
            {
                return null;
            }

            var claim = new AgentInstanceLock(mutex, name);
            mutex = null;
            return claim;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // Held by an elevated process, or the name is taken by an object that is not a mutex: either way, not ours.
            return null;
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    /// <summary>True while any process, this one included, holds the claim on <paramref name="dataDirectory"/>.</summary>
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

namespace FrameLedger.App.Services;

/// <summary><c>07_IPC</c> §Client behavior's numbers, overridable so a test does not wait on the product's clock.</summary>
public sealed record AgentConnectionOptions
{
    /// <summary>"Connect with 250 ms × 8 backoff".</summary>
    public int ConnectAttempts { get; init; } = 8;

    public TimeSpan ConnectBackoff { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>After a round that found no Agent (started or not): how long before the next round.</summary>
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary><c>Ping</c> cadence while connected (07_IPC: 15 s keepalive, client-driven).</summary>
    public TimeSpan Keepalive { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

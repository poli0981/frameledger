using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary>An <see cref="IUpdateClient"/> a test scripts: what the next check finds, what each step throws, and what was asked of it.</summary>
internal sealed class FakeUpdateClient : IUpdateClient
{
    public bool IsInstalled { get; set; } = true;

    public string? CurrentVersion { get; set; } = "0.1.0";

    /// <summary>What the next check returns; null is "current".</summary>
    public UpdateCandidate? Next { get; set; }

    public UpdateException? CheckThrows { get; set; }

    /// <summary>One failure per download attempt, in order; an empty queue downloads.</summary>
    public Queue<UpdateException> DownloadThrows { get; } = new();

    public int DownloadAttempts { get; private set; }

    /// <summary>The <c>includePrereleases</c> flag of every check, in order.</summary>
    public List<bool> Checks { get; } = [];

    public List<string> Downloaded { get; } = [];

    public List<string> Applied { get; } = [];

    public Task<UpdateCandidate?> CheckAsync(bool includePrereleases, CancellationToken ct = default)
    {
        Checks.Add(includePrereleases);
        return CheckThrows is { } ex ? Task.FromException<UpdateCandidate?>(ex) : Task.FromResult(Next);
    }

    public Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default)
    {
        DownloadAttempts++;
        if (DownloadThrows.TryDequeue(out UpdateException? ex))
        {
            return Task.FromException(ex);
        }

        progress?.Report(50);
        progress?.Report(100);
        Downloaded.Add(candidate.Version);
        return Task.CompletedTask;
    }

    public void ApplyOnExit(UpdateCandidate candidate) => Applied.Add(candidate.Version);
}

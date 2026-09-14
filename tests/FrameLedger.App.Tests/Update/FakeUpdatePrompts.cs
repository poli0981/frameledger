using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary>An <see cref="IUpdatePrompts"/> that answers the offer as scripted and remembers every dialog it would have shown.</summary>
internal sealed class FakeUpdatePrompts : IUpdatePrompts
{
    public bool Accept { get; set; } = true;

    public List<string> Offered { get; } = [];

    public List<string> UpToDate { get; } = [];

    public int NotInstalled { get; private set; }

    public List<UpdateFailure> Failed { get; } = [];

    public Task<bool> OfferAsync(UpdateCandidate candidate, CancellationToken ct = default)
    {
        Offered.Add(candidate.Version);
        return Task.FromResult(Accept);
    }

    public Task UpToDateAsync(string version, CancellationToken ct = default)
    {
        UpToDate.Add(version);
        return Task.CompletedTask;
    }

    public Task NotInstalledAsync(CancellationToken ct = default)
    {
        NotInstalled++;
        return Task.CompletedTask;
    }

    public Task FailedAsync(UpdateFailure failure, CancellationToken ct = default)
    {
        Failed.Add(failure);
        return Task.CompletedTask;
    }
}

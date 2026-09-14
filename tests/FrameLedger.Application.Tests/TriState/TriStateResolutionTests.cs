using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.TriState;

/// <summary>FR-8.3's precedence: manual, then measured, then the game's default marked inherited, then N/A.</summary>
public sealed class TriStateResolutionTests
{
    [Theory]
    [InlineData(Tri.No, Tri.Yes, Tri.Yes, Tri.No, TriStateSource.Manual)]
    [InlineData(Tri.NotApplicable, Tri.Yes, Tri.No, Tri.NotApplicable, TriStateSource.Manual)]
    [InlineData(null, Tri.Yes, Tri.No, Tri.Yes, TriStateSource.Measured)]
    [InlineData(null, Tri.No, Tri.Yes, Tri.No, TriStateSource.Measured)]
    [InlineData(null, Tri.NotApplicable, Tri.Yes, Tri.Yes, TriStateSource.Inherited)]
    [InlineData(null, Tri.NotApplicable, Tri.NotApplicable, Tri.NotApplicable, TriStateSource.NotApplicable)]
    public void TheRule(Tri? manual, Tri measured, Tri gameDefault, Tri expected, TriStateSource source)
    {
        TriStateResolution.Resolve(manual, measured, gameDefault).Should().Be(new ResolvedTriState(expected, source));
    }

    [Fact]
    public void AnExplicitManualNotApplicableIsStillManualNotAFallThrough()
    {
        // The user saying "N/A" is a statement (this title's RT is not classifiable), not the absence of one.
        TriStateResolution.Resolve(Tri.NotApplicable, Tri.Yes, Tri.Yes).Source.Should().Be(TriStateSource.Manual);
    }

    [Fact]
    public void OverStoredRowsEachKindReadsItsOwnColumns()
    {
        var game = new GameRow
        {
            Id = 1,
            Name = "T",
            Fingerprint = new ExecutableFingerprint { ExePath = @"C:\t.exe", SizeBytes = 1, MtimeUnixMs = 1 },
            HookEnabled = false,
            HookCrashCount = 0,
            AddedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            RtDefault = Tri.NotApplicable,
            PtDefault = Tri.Yes,
            RrDefault = Tri.No,
        };
        var session = new SessionRow
        {
            SessionGuid = Guid.NewGuid(),
            GameId = 1,
            SnapshotId = 1,
            StartedAt = DateTimeOffset.UnixEpoch,
            EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            QpcEpoch = 0,
            QpcFrequency = 1,
            Tier = CaptureTier.Hooked,
            Mode = CaptureMode.Attach,
            ExitStatus = ExitStatus.Normal,
            RtFlag = "yes",
            RtSource = "measured",
            PtFlag = "na",
            RrFlag = "na",
        };
        var annotation = new SessionAnnotation { SessionId = 1, RrOverride = Tri.Yes };

        TriStateResolution.Resolve(TriStateKind.RayTracing, session, annotation, game).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Measured));
        TriStateResolution.Resolve(TriStateKind.PathTracing, session, annotation, game).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Inherited));
        TriStateResolution.Resolve(TriStateKind.RayReconstruction, session, annotation, game).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Manual));
        TriStateResolution.Resolve(TriStateKind.RayReconstruction, session, null, game).Should().Be(new ResolvedTriState(Tri.No, TriStateSource.Inherited), "no annotation row at all");
    }
}

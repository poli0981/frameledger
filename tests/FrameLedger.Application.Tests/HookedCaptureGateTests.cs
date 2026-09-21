using System.Reflection;
using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests;

public sealed class HookedCaptureGateTests
{
    private sealed class RecordingGuard : IAntiCheatGuard
    {
        public int InjectCalls { get; private set; }
        public AntiCheatVerdict Next { get; set; } = AntiCheatVerdict.Allowed();

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, CancellationToken ct = default) =>
            ValueTask.FromResult(Next);

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath,
            CancellationToken ct = default)
        {
            InjectCalls++;
            return ValueTask.FromResult(Next);
        }

        public int WhenReadyCalls { get; private set; }

        public int WhenReadyWaitMs { get; private set; }

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath,
            int timeoutMs, CancellationToken ct = default)
        {
            WhenReadyCalls++;
            WhenReadyWaitMs = timeoutMs;
            return ValueTask.FromResult(Next);
        }

        /// <summary>The user's bypass (owner decision 2026-09-21): which entry the gate chose, and what it answers there.</summary>
        public int AcknowledgedCalls { get; private set; }

        public int AcknowledgedWhenReadyCalls { get; private set; }

        public AntiCheatVerdict NextAcknowledged { get; set; } =
            AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.AllowedUnderUserBypass, "Easy Anti-Cheat", "EasyAntiCheat_EOS.dll");

        public ValueTask<AntiCheatVerdict> GuardedInjectAcknowledgedAsync(int targetPid, string payloadPath, CancellationToken ct = default)
        {
            AcknowledgedCalls++;
            return ValueTask.FromResult(NextAcknowledged);
        }

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAcknowledgedAsync(int targetPid, string payloadPath, int timeoutMs,
            CancellationToken ct = default)
        {
            AcknowledgedWhenReadyCalls++;
            return ValueTask.FromResult(NextAcknowledged);
        }

        public int PreScanCalls { get; private set; }

        public ValueTask<AntiCheatVerdict> PreScanGameDirectoryAsync(string gameDirectory,
            CancellationToken ct = default)
        {
            PreScanCalls++;
            return ValueTask.FromResult(Next);
        }
    }

    private static ExecutableFingerprint OnDisk =>
        new() { ExePath = @"C:\Games\Title\game.exe", SizeBytes = 90_000, MtimeUnixMs = 1_700_000_000_000 };

    // Requests are built the way production builds them — from a stored record through
    // HookRequest.FromConsent — because there is no other way any more. `new HookRequest { ... }` was
    // §S27's rejected synthesis, and this fixture used to be written in exactly that shape, so the
    // fixture itself was demonstrating the hole the gate exists to close.
    //
    // `provenance: NotRecorded` is the only way to express ABSENT consent, and it has to stay
    // expressible: the earlier fixture's `consent ?? DateTimeOffset.UnixEpoch` meant passing null
    // produced a request that HAD consented, so the case FR-2.1 exists for could not be written down.
    private static HookRequest Request(
        bool enabled = true,
        ConsentProvenance provenance = ConsentProvenance.UnshippedHostOperator,
        string? blocked = null,
        int waitMs = 0,
        bool killSwitch = false,
        bool bypass = false) =>
        HookRequest.FromConsent(
            GameConsentRecord.Stored(
                OnDisk, enabled, DateTimeOffset.UnixEpoch, provenance, "unshipped-host-operator/1",
                blocked, preScanUnverified: false, updatedAt: DateTimeOffset.UnixEpoch)
                .WithGuardBypass(bypass ? DateTimeOffset.UnixEpoch : null, bypass ? "guard-bypass-dialog/1" : null),
            OnDisk,
            targetPid: 1234,
            payloadPath: @"C:\FrameLedger\FrameLedger.Overlay.dll",
            waitMs,
            killSwitch);

    [Fact]
    public async Task TheKillSwitchIsTheFourthInputAndOutranksAValidConsent()
    {
        // FR-2.4 (P2 PR-F, HANDOFF §P2 decision D7): a fully consented, enabled, unblocked game is refused
        // with its own reason when the global switch is on, and the guard is never asked — in either entry.
        var guard = new RecordingGuard();
        var gate = new HookedCaptureGate(guard);
        CancellationToken ct = TestContext.Current.CancellationToken;

        AntiCheatVerdict attach = await gate.StartAsync(Request(killSwitch: true), ct);
        AntiCheatVerdict launch = await gate.StartAsync(Request(waitMs: 60_000, killSwitch: true), ct);

        attach.Reason.Should().Be(AntiCheatRefusalReason.KillSwitchEngaged);
        launch.Reason.Should().Be(AntiCheatRefusalReason.KillSwitchEngaged);
        attach.IsAllowed.Should().BeFalse();
        guard.InjectCalls.Should().Be(0);
        guard.WhenReadyCalls.Should().Be(0);

        // Off again: the same record reaches the guard, so the switch decided and not the record.
        (await gate.StartAsync(Request(), ct)).IsAllowed.Should().BeTrue();
        guard.InjectCalls.Should().Be(1);
    }

    /// <summary>
    /// THE FIFTH INPUT (owner decision 2026-09-21, 19_SAFETY §The user's bypass). It overrules the guard's judgement —
    /// the latch of a past one, and the live one — and NOTHING above it: the kill switch, a game that was merely added
    /// and a consent nobody gave each still refuse, with the guard never asked through either kind of entry.
    /// </summary>
    [Fact]
    public async Task TheBypassOverrulesTheGuardsJudgementAndNothingAboveIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var guard = new RecordingGuard();
        var gate = new HookedCaptureGate(guard);

        (await gate.StartAsync(Request(bypass: true, killSwitch: true), ct)).Reason.Should().Be(AntiCheatRefusalReason.KillSwitchEngaged);
        (await gate.StartAsync(Request(bypass: true, enabled: false), ct)).Reason.Should().Be(AntiCheatRefusalReason.HookNotEnabled);
        (await gate.StartAsync(Request(bypass: true, provenance: ConsentProvenance.NotRecorded), ct)).Reason.Should().Be(AntiCheatRefusalReason.ConsentMissing);
        (guard.InjectCalls + guard.WhenReadyCalls + guard.AcknowledgedCalls + guard.AcknowledgedWhenReadyCalls).Should().Be(0, "none of the three reached the guard, by any entry");

        // A blocked row without the bypass: the latch, as always.
        (await gate.StartAsync(Request(blocked: "BlockedModule: Easy Anti-Cheat"), ct)).Reason.Should().Be(AntiCheatRefusalReason.PreviouslyBlocked);
        guard.AcknowledgedCalls.Should().Be(0);

        // The same row with it: past the latch, through the ACKNOWLEDGED entry, and the answer is not an allow.
        AntiCheatVerdict attach = await gate.StartAsync(Request(blocked: "BlockedModule: Easy Anti-Cheat", bypass: true), ct);
        attach.Reason.Should().Be(AntiCheatRefusalReason.AllowedUnderUserBypass);
        attach.IsAllowed.Should().BeFalse("a caller that only knows IsAllowed still does not proceed");
        attach.IsAllowedUnderBypass.Should().BeTrue();
        attach.Family.Should().Be("Easy Anti-Cheat", "the verdict still names what the guard found");
        guard.AcknowledgedCalls.Should().Be(1);
        guard.InjectCalls.Should().Be(0, "an acknowledged request never takes the unacknowledged entry, and the reverse");

        AntiCheatVerdict launch = await gate.StartAsync(Request(waitMs: 60_000, bypass: true), ct);
        launch.IsAllowedUnderBypass.Should().BeTrue();
        guard.AcknowledgedWhenReadyCalls.Should().Be(1);
        guard.WhenReadyCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnderTheBypassAFactStillRefusesAndACleanTargetIsAPlainAllow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var guard = new RecordingGuard { NextAcknowledged = AntiCheatVerdict.Refused(AntiCheatRefusalReason.PayloadNotOurs, string.Empty, "not ours") };
        var gate = new HookedCaptureGate(guard);

        AntiCheatVerdict foreign = await gate.StartAsync(Request(bypass: true), ct);
        foreign.Reason.Should().Be(AntiCheatRefusalReason.PayloadNotOurs, "no acknowledgement loads a foreign DLL");
        foreign.IsAllowedUnderBypass.Should().BeFalse();

        guard.NextAcknowledged = AntiCheatVerdict.Allowed();
        (await gate.StartAsync(Request(bypass: true), ct)).IsAllowed.Should().BeTrue("nothing to overrule: a clean target is a plain allow");
    }

    [Fact]
    public async Task AGuardThatDoesNotKnowTheBypassIsTheHardGate()
    {
        // IAntiCheatGuard's acknowledged methods default to the unacknowledged ones: a fake, an older adapter or a
        // forgotten override fails towards refusing.
        var guard = new DefaultsOnlyGuard();
        AntiCheatVerdict v = await new HookedCaptureGate(guard).StartAsync(Request(bypass: true), TestContext.Current.CancellationToken);

        v.Reason.Should().Be(AntiCheatRefusalReason.BlockedModule);
        v.IsAllowedUnderBypass.Should().BeFalse();
    }

    private sealed class DefaultsOnlyGuard : IAntiCheatGuard
    {
        private static readonly AntiCheatVerdict _refusal = AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "Easy Anti-Cheat", "x.dll");

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, CancellationToken ct = default) => ValueTask.FromResult(_refusal);

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, CancellationToken ct = default) => ValueTask.FromResult(_refusal);

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, CancellationToken ct = default) => ValueTask.FromResult(_refusal);

        public ValueTask<AntiCheatVerdict> PreScanGameDirectoryAsync(string gameDirectory, CancellationToken ct = default) => ValueTask.FromResult(_refusal);
    }

    [Fact]
    public async Task AnEnabledConsentedGame_ReachesTheGuard()
    {
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(Request(), TestContext.Current.CancellationToken);

        v.IsAllowed.Should().BeTrue();
        guard.InjectCalls.Should().Be(1);
        guard.WhenReadyCalls.Should().Be(0, "attach mode injects now, not after a wait");
    }

    [Fact]
    public async Task ALaunchModeRequest_ReachesTheGuardThroughTheWaitingEntryOnly()
    {
        // P1 item 2. The consent checks are the same three, in the same order; the only thing a wait
        // changes is WHICH guard entry runs the full scan -- and it must never be both.
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(Request(waitMs: 60_000), TestContext.Current.CancellationToken);

        v.IsAllowed.Should().BeTrue();
        guard.WhenReadyCalls.Should().Be(1);
        guard.WhenReadyWaitMs.Should().Be(60_000);
        guard.InjectCalls.Should().Be(0);
    }

    [Fact]
    public async Task ALaunchModeRequestWithoutConsent_ReachesNeitherEntry()
    {
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(Request(provenance: ConsentProvenance.NotRecorded, waitMs: 60_000),
            TestContext.Current.CancellationToken);

        v.Reason.Should().Be(AntiCheatRefusalReason.ConsentMissing);
        guard.WhenReadyCalls.Should().Be(0);
        guard.InjectCalls.Should().Be(0);
    }

    [Fact]
    public async Task HookingOff_NeverReachesTheGuard()
    {
        // CLAUDE.md rule 1: nothing is injected because a game was merely
        // added. The guard is not even asked.
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(Request(enabled: false), TestContext.Current.CancellationToken);

        v.IsAllowed.Should().BeFalse();
        guard.InjectCalls.Should().Be(0);
    }

    [Fact]
    public async Task NoConsent_NeverReachesTheGuard()
    {
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(
            Request(provenance: ConsentProvenance.NotRecorded), TestContext.Current.CancellationToken);

        v.IsAllowed.Should().BeFalse();
        guard.InjectCalls.Should().Be(0);
    }

    [Fact]
    public async Task APreviouslyBlockedGame_NeverReachesTheGuard()
    {
        // 19_SAFETY: a game enabled before it started matching is force-disabled
        // on the next rules update. Honouring hook_blocked_reason here means a
        // stale in-memory watchlist cannot resurrect it.
        RecordingGuard guard = new();
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(
            Request(blocked: "EasyAntiCheat/ appeared in the game directory"), TestContext.Current.CancellationToken);

        v.IsAllowed.Should().BeFalse();
        v.Signal.Should().Contain("EasyAntiCheat");
        guard.InjectCalls.Should().Be(0);
    }

    [Fact]
    public async Task AGuardRefusal_IsPassedThroughUnchanged()
    {
        // The gate adds no judgement of its own about anti-cheat. Whatever the
        // guard said is what the caller sees.
        RecordingGuard guard = new()
        {
            Next = AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedDriver, "Riot Vanguard", "vgk.sys"),
        };
        HookedCaptureGate gate = new(guard);

        AntiCheatVerdict v = await gate.StartAsync(Request(), TestContext.Current.CancellationToken);

        v.Reason.Should().Be(AntiCheatRefusalReason.BlockedDriver);
        v.Family.Should().Be("Riot Vanguard");
    }

    // WHAT REPLACED TWO ShouldUnhook FACTS, and why the replacement is stronger.
    //
    // `ShouldUnhook_IsTrueForAnyRefusal` and `ShouldUnhook_IsFalseOnlyWhenAllowed`
    // lived here and asserted the boolean only. They never asserted the two
    // properties whose ABSENCE was the defect (§S29(c)): the method published no
    // guardTicks and did not latch. So the tests certified the API as sanctioned
    // while saying nothing about the thing that was wrong with it — and a drain
    // loop already holds this object, which made the weaker of the two re-scan
    // APIs the more discoverable one.
    //
    // GuardSupervisorTests already covers the re-scan, the tick and the latch.
    // What was missing was anything that goes red when a SECOND route reappears,
    // which is what this asserts. It is red on unmodified main, where the gate
    // declares two public instance methods.
    [Fact]
    public void TheGateExposesNoSecondInSessionRescanPath()
    {
        IEnumerable<string> declared = typeof(HookedCaptureGate)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name);

        declared.Should().BeEquivalentTo(
            ["StartAsync"],
            "the in-session re-scan belongs to GuardSupervisor, which publishes a tick at exactly one "
            + "site and latches its refusal (20_OPEN_QUESTIONS §S29(c)). A second route on this class "
            + "has neither property and is the more discoverable one, because a drain loop is already "
            + "holding the gate.");
    }

    // Each managed refusal must be DISTINGUISHABLE. All three used to return
    // BlockedExecutable — check 3's code, which at the time the native guard
    // could not produce at all (§S14), so a user who had not accepted the
    // consent dialog would have been told the title was on the per-title
    // blocklist. Nothing asserted the reason, which is why nothing noticed.
    //
    // #52 wired check 3's executable half, so the native guard produces
    // BlockedExecutable now. That strengthens this test rather than dating it:
    // the code is no longer available as a catch-all, because it now means one
    // specific thing.
    [Fact]
    public async Task EachManagedRefusal_CarriesItsOwnReason()
    {
        var guard = new RecordingGuard();
        var gate = new HookedCaptureGate(guard);

        CancellationToken ct = TestContext.Current.CancellationToken;
        AntiCheatVerdict notEnabled = await gate.StartAsync(Request(enabled: false), ct);
        AntiCheatVerdict noConsent = await gate.StartAsync(Request(provenance: ConsentProvenance.NotRecorded), ct);
        AntiCheatVerdict blocked = await gate.StartAsync(Request(blocked: "EasyAntiCheat appeared after a patch"), ct);
        AntiCheatVerdict killed = await gate.StartAsync(Request(killSwitch: true), ct);

        notEnabled.Reason.Should().Be(AntiCheatRefusalReason.HookNotEnabled);
        noConsent.Reason.Should().Be(AntiCheatRefusalReason.ConsentMissing);
        blocked.Reason.Should().Be(AntiCheatRefusalReason.PreviouslyBlocked);
        killed.Reason.Should().Be(AntiCheatRefusalReason.KillSwitchEngaged);

        // ...and none of them borrows a reason the native guard owns.
        new[] { notEnabled.Reason, noConsent.Reason, blocked.Reason, killed.Reason }
            .Should().OnlyHaveUniqueItems()
            .And.NotContain(AntiCheatRefusalReason.BlockedExecutable);

        guard.InjectCalls.Should().Be(0, "a refusal here must never reach the guard, let alone the primitive");
    }

    [Fact]
    public void ANullGuard_IsRejected()
    {
        Action act = () => _ = new HookedCaptureGate(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

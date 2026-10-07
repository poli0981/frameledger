// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.18: a notice is made from what the Agent sends, so these are made from what the Agent sends — the gate's own
/// verdicts, the verdicts the native guard's facade builds (<see cref="AntiCheatVerdict.FromNative"/>), published by
/// <see cref="RecordedSessionEvents.PublishReason"/> through the wire's codec — never an event written by hand. Hand-written
/// events had been the notices' only tests ("GuardBlocked", "HookFaulted", a guard reason sent as the end) while the Agent's
/// own read "The hook stopped (WriterSelfDisabled)…", "kill switch was detected in this game (… (FR-2.4) …)" and "Access is
/// denied was detected while the game was running".
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class NoticesFromTheAgentTests
{
    private static readonly ExecutableFingerprint _onDisk = new() { ExePath = @"C:\Games\Title\game.exe", SizeBytes = 90_000, MtimeUnixMs = 1_700_000_000_000 };

    /// <summary>The gate's refusal of a game blocked before, labelled as <c>HookedCaptureGate</c> labels it.</summary>
    private static readonly AntiCheatVerdict _blockedBefore =
        AntiCheatVerdict.Refused(AntiCheatRefusalReason.PreviouslyBlocked, "previously blocked", "AntiCheatFile|NetEase Yidun|NEP2.dll");

    /// <summary>What a user must never read in a notice: the enums' names, a requirement's id, the fallback's "reason X".</summary>
    private static readonly Regex[] _internal =
    [
        .. Enum.GetNames<SessionEndReason>().Concat(Enum.GetNames<AntiCheatRefusalReason>()).Select(static n => Pattern(@"\b" + n + @"\b")),
        Pattern(@"FR-\d"),
        Pattern(@"\breason [A-Z]"),
    ];

    /// <summary>
    /// "kill switch was detected in this game (the global 'disable all hooking' switch is on (FR-2.4); …)" until beta.18. The
    /// switch is checked before anything else, so the gate refuses even a record nobody stored, and its guard is never asked.
    /// </summary>
    [Fact]
    public async Task DisableAllHookingIsTheUsersSwitchNeverAFinding()
    {
        AntiCheatVerdict gate = await new HookedCaptureGate(new UnaskedGuard())
            .StartAsync(HookRequest.FromConsent(default, _onDisk, targetPid: 4242, @"C:\FrameLedger\FrameLedger.Overlay.dll", killSwitchEngaged: true),
                TestContext.Current.CancellationToken);

        SafetyNotice notice = InEnglish(() => Notice(new CaptureOutcome { Reason = SessionEndReason.RefusedKillSwitch, Verdict = gate }));

        gate.Family.Should().Be("kill switch", "the gate labels its own refusal in the family's place — which is what the notice printed");
        notice.Body.Should().Be("All hooking is switched off in Settings, so nothing was measured. " + Strings.Notice_Refused_Recording);
        Leaks(notice).Should().BeEmpty();
    }

    /// <summary>
    /// A game with its user-mode exception granted and the option since turned off (D33): hooking is on, the row is blocked,
    /// no family is tolerated. The verdict is the gate's as <c>HookedCaptureGate</c> labels it — a stored record is not a
    /// test's to make (Domain's InternalsVisibleTo).
    /// </summary>
    [Fact]
    public void AGameBlockedBeforeIsSaidAsThatNeverAsAFindingNamedPreviouslyBlocked()
    {
        SafetyNotice notice = InEnglish(() => Notice(new CaptureOutcome { Reason = SessionEndReason.RefusedPreviouslyBlocked, Verdict = _blockedBefore }));

        notice.Body.Should().StartWith("Anti-cheat was found in this game before, so it is never hooked.").And.NotContain("previously blocked was detected");
        Leaks(notice).Should().BeEmpty();
    }

    [Fact]
    public void AGuardRefusalThatNamesNoFamilyIsSaidInTheGuardsWords()
    {
        AntiCheatVerdict wow64 = AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.TargetIsWow64, string.Empty, "target is a WOW64 process");

        SafetyNotice notice = InEnglish(() => Notice(new CaptureOutcome { Reason = SessionEndReason.RefusedByGuard, Verdict = wow64 }));

        notice.Body.Should().StartWith("The anti-cheat guard did not hook this game: the game is 32-bit, and the hook runs in 64-bit games only.");
        Leaks(notice).Should().BeEmpty();
    }

    /// <summary>An agent from before beta.18 sent no guard reason beside no family: the end's own words, not its name.</summary>
    [Fact]
    public void AnOlderAgentsFamilylessRefusalIsStillWords()
    {
        SafetyNotice notice = InEnglish(() => new SafetyNotices(new FakeAgentLink(), new RecordingStrip()).Translate(
            FakeAgentLink.Envelope(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", null, "target is a WOW64 process")))!);

        notice.Body.Should().StartWith("The anti-cheat guard refused to hook this game.");
        Leaks(notice).Should().BeEmpty();
    }

    /// <summary>The control: a finding is still said as one, by its family and signal.</summary>
    [Fact]
    public void AFindingIsStillNamed()
    {
        AntiCheatVerdict found = AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll");

        SafetyNotice notice = InEnglish(() => Notice(new CaptureOutcome { Reason = SessionEndReason.RefusedByGuard, Verdict = found, HookingTurnedOff = true }));

        notice.Body.Should().StartWith("BattlEye was detected in this game (BEClient_x64.dll).");
        Leaks(notice).Should().BeEmpty();
    }

    /// <summary>The 30 s re-scan could not read the game's process while it still ran (an exit is relabelled, D48(b)).</summary>
    [Fact]
    public void AReScanThatCouldNotLookIsNotAFinding()
    {
        AntiCheatVerdict unreadable = AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.ProcessUnreadable, string.Empty, "Access is denied.");

        SafetyNotice notice = InEnglish(() => Notice(new CaptureOutcome { Reason = ExitScanRelabel.EndOf(unreadable, targetExited: false), Verdict = unreadable }));

        notice.Kind.Should().Be(SafetyNoticeKind.Unhooked);
        notice.Body.Should().Be("Capture stopped while the game was running — the game's process could not be opened — and FrameLedger unhooked.");
        Leaks(notice).Should().BeEmpty();
    }

    /// <summary>Every end that stops measuring mid-session is said in its own sentence, and never as continuing.</summary>
    [Fact]
    public void EveryStopMidSessionIsItsOwnSentence()
    {
        List<SessionEndReason> stops = [.. Enum.GetValues<SessionEndReason>().Where(static r => RecordedSessionEvents.Classify(r) == RecordedSessionEvents.Kind.Degraded)];

        List<string> wrong = InEnglish(() => stops
            .Select(r => (Reason: r, Notice: Notice(new CaptureOutcome { Reason = r, Verdict = AntiCheatVerdict.Allowed() })))
            .Where(static x => !string.Equals(x.Notice.Body, Formats.EndReasonText(x.Reason.ToString()), StringComparison.Ordinal) || Leaks(x.Notice).Count > 0
                               || x.Notice.Body.Contains("continues", StringComparison.Ordinal))
            .Select(static x => $"{x.Reason}: \"{x.Notice.Body}\"")
            .ToList());

        stops.Should().Contain([SessionEndReason.WriterSelfDisabled, SessionEndReason.KillSwitchEngaged]);
        string.Join(Environment.NewLine, wrong).Should().BeEmpty();
    }

    /// <summary>Every refusal the Agent publishes, each with the verdict it comes with, reads without an internal name.</summary>
    [Fact]
    public async Task EveryRefusalReadsAsWords()
    {
        AntiCheatVerdict killSwitch = await new HookedCaptureGate(new UnaskedGuard())
            .StartAsync(HookRequest.FromConsent(default, _onDisk, targetPid: 4242, @"C:\FrameLedger\FrameLedger.Overlay.dll", killSwitchEngaged: true),
                TestContext.Current.CancellationToken);
        List<SessionEndReason> refusals = [.. Enum.GetValues<SessionEndReason>().Where(static r => RecordedSessionEvents.Classify(r) == RecordedSessionEvents.Kind.Refused)];

        List<string> wrong = InEnglish(() => refusals
            .Select(r => (Reason: r, Notice: Notice(new CaptureOutcome
            {
                Reason = r,
                Verdict = r switch
                {
                    SessionEndReason.RefusedKillSwitch => killSwitch,
                    SessionEndReason.RefusedPreviouslyBlocked => _blockedBefore,
                    SessionEndReason.RefusedByGuard => AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.ModuleScanFailed, string.Empty, "Only part of a ReadProcessMemory request was completed."),
                    _ => default,
                },
            })))
            .Where(static x => Leaks(x.Notice).Count > 0)
            .Select(static x => $"{x.Reason}: \"{x.Notice.Body}\" ({string.Join(", ", Leaks(x.Notice))})")
            .ToList());

        refusals.Should().HaveCountGreaterThanOrEqualTo(5);
        string.Join(Environment.NewLine, wrong).Should().BeEmpty();
    }

    /// <summary>The one safety event <paramref name="outcome"/> publishes, over the wire's codec, as the App's notice.</summary>
    private static SafetyNotice Notice(CaptureOutcome outcome)
    {
        var pipe = new Pipe();
        RecordedSessionEvents.PublishReason(pipe, Guid.NewGuid(), outcome,
            new SessionStartedInfo(Guid.NewGuid(), 7, "Title", _onDisk.ExePath, CaptureMode.Attach, DateTimeOffset.UnixEpoch, 10_000_000));
        IpcEnvelope sent = pipe.Sent.Should().ContainSingle($"{outcome.Reason} publishes one event").Subject;
        using var notices = new SafetyNotices(new FakeAgentLink(), new RecordingStrip());
        return notices.Translate(sent) ?? throw new InvalidOperationException($"{sent.Type} made no notice");
    }

    private static List<string> Leaks(SafetyNotice notice) => [.. _internal.Select(r => r.Match(notice.Body)).Where(static m => m.Success).Select(static m => m.Value)];

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? app = Strings.Culture;
        CultureInfo? shared = Shared.Strings.Culture;
        CultureInfo current = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            Shared.Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            return f();
        }
        finally
        {
            Strings.Culture = app;
            Shared.Strings.Culture = shared;
            CultureInfo.CurrentCulture = current;
        }
    }

    private sealed class Pipe : IIpcEventPublisher
    {
        public List<IpcEnvelope> Sent { get; } = [];

        public bool HasClients => true;

        public void Publish<T>(string type, T payload)
            where T : class => Sent.Add(IpcCodec.Decode(IpcCodec.Encode(type, null, payload)));
    }

    /// <summary>A guard that must not be reached: every refusal these tests take from the gate is the gate's own.</summary>
    private sealed class UnaskedGuard : IAntiCheatGuard
    {
        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, string? toleratedFamily, CancellationToken ct = default) => throw Asked();

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, string? toleratedFamily, CancellationToken ct = default) => throw Asked();

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, string? toleratedFamily,
            CancellationToken ct = default) => throw Asked();

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, string? toleratedFamily, CancellationToken ct = default) => throw Asked();

        private static InvalidOperationException Asked() => new("the gate asked the guard");
    }
}

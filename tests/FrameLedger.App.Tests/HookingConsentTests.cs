using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;

namespace FrameLedger.App.Tests;

/// <summary>
/// FR-2.1 from the App's side: the version handshake gates the dialog (D14), a declined dialog sends nothing,
/// an acknowledged one sends exactly <c>SetHookEnabled { enabled: true, disclosureVersion: ours }</c>, and the
/// Agent's three answers (stamped, refused, error) come back as what they are.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class HookingConsentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HelloAck Hello(string? disclosure) => new("agent", IpcProtocol.Version, 1, false, "b", false, "l1", false, disclosure);

    private sealed class FakeAgent : IAgentRequests
    {
        public HelloAck? Hello { get; set; }

        public bool IsConnected { get; set; } = true;

        public List<(string Type, object Payload)> Sent { get; } = [];

        public Func<object, IpcEnvelope> Answer { get; set; } = static _ => throw new InvalidOperationException("no answer scripted");

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class
        {
            Sent.Add((type, payload));
            return Task.FromResult(Answer(payload));
        }
    }

    private sealed class FakePrompt : IConsentPrompt
    {
        public bool Accepts { get; set; }

        public List<string> Shown { get; } = [];

        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default)
        {
            Shown.Add(gameName);
            return Task.FromResult(Accepts);
        }
    }

    private static IpcEnvelope Ack<T>(string type, T payload) where T : class => IpcCodec.Decode(IpcCodec.Encode(type, "1", payload));

    [Fact]
    public async Task WithoutAConnectedAgentNothingIsShownOrSent()
    {
        var agent = new FakeAgent { Hello = null, IsConnected = false };
        var prompt = new FakePrompt { Accepts = true };
        var consent = new HookingConsent(agent, prompt);

        (await consent.EnableAsync(7, "Title", Ct)).Outcome.Should().Be(HookingConsentOutcome.AgentUnavailable);
        (await consent.DisableAsync(7, Ct)).Outcome.Should().Be(HookingConsentOutcome.AgentUnavailable);
        prompt.Shown.Should().BeEmpty();
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAgentThatStampsAgainstOtherTextGetsNoDialogAndNoRequest()
    {
        var agent = new FakeAgent { Hello = Hello("consent-dialog/0") };
        var prompt = new FakePrompt { Accepts = true };

        HookingConsentResult result = await new HookingConsent(agent, prompt).EnableAsync(7, "Title", Ct);

        result.Outcome.Should().Be(HookingConsentOutcome.VersionMismatch, "D14: a dialog whose Enable cannot mean what it says is not shown");
        result.Detail.Should().Be("consent-dialog/0");
        prompt.Shown.Should().BeEmpty();
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ADeclinedDialogSendsNothing()
    {
        var agent = new FakeAgent { Hello = Hello(SafetyDisclosure.Version) };
        var prompt = new FakePrompt { Accepts = false };

        (await new HookingConsent(agent, prompt).EnableAsync(7, "Title", Ct)).Outcome.Should().Be(HookingConsentOutcome.Declined);
        prompt.Shown.Should().Equal("Title");
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAcknowledgedDialogSendsOurVersionAndTheAgentsStampIsEnabled()
    {
        var agent = new FakeAgent { Hello = Hello(SafetyDisclosure.Version) };
        agent.Answer = static p => Ack(IpcMessageType.HookEnabledAck, new HookEnabledAck(((SetHookEnabledRequest)p).GameId, true, "Written", "clean"));
        var prompt = new FakePrompt { Accepts = true };

        HookingConsentResult result = await new HookingConsent(agent, prompt).EnableAsync(7, "Title", Ct);

        result.Outcome.Should().Be(HookingConsentOutcome.Enabled);
        result.Detail.Should().Be("Written");
        agent.Sent.Should().ContainSingle().Which.Should().Be((IpcMessageType.SetHookEnabled, (object)new SetHookEnabledRequest(7, true, SafetyDisclosure.Version)));
    }

    [Fact]
    public async Task ARefusalIsARefusalWithItsSignalAndAStoreOutcomeOtherThanWrittenIsAFailure()
    {
        var agent = new FakeAgent { Hello = Hello(SafetyDisclosure.Version) };
        var prompt = new FakePrompt { Accepts = true };
        var consent = new HookingConsent(agent, prompt);

        agent.Answer = static _ => Ack(IpcMessageType.Refused, new RefusedAck(7, "BlockedModule", "eac", "EasyAntiCheat_EOS.dll"));
        HookingConsentResult refused = await consent.EnableAsync(7, "Title", Ct);
        refused.Outcome.Should().Be(HookingConsentOutcome.Refused);
        refused.Refusal!.Signal.Should().Be("EasyAntiCheat_EOS.dll");
        refused.RefusalText().Should().Contain("eac").And.Contain("EasyAntiCheat_EOS.dll").And.Contain("no way to override");

        agent.Answer = static _ => Ack(IpcMessageType.Refused, new RefusedAck(7, "PreScanCouldNotVerify", null, null));
        (await consent.EnableAsync(7, "Title", Ct)).RefusalText().Should().Be(Shared.Strings.Safety_Refused_CouldNotVerify);

        agent.Answer = static _ => Ack(IpcMessageType.HookEnabledAck, new HookEnabledAck(7, false, "StaleFingerprint", "clean"));
        HookingConsentResult failed = await consent.EnableAsync(7, "Title", Ct);
        failed.Outcome.Should().Be(HookingConsentOutcome.Failed);
        failed.Detail.Should().Be("StaleFingerprint");
    }

    [Fact]
    public async Task AnErrorFromTheAgentIsAFailureExceptTheMismatchWhichIsNamed()
    {
        var agent = new FakeAgent { Hello = Hello(SafetyDisclosure.Version) };
        var consent = new HookingConsent(agent, new FakePrompt { Accepts = true });

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.DisclosureVersionMismatch, "restart both");
        (await consent.EnableAsync(7, "Title", Ct)).Outcome.Should().Be(HookingConsentOutcome.VersionMismatch);

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.UnknownGame, "no such row");
        HookingConsentResult failed = await consent.EnableAsync(7, "Title", Ct);
        failed.Outcome.Should().Be(HookingConsentOutcome.Failed);
        failed.Detail.Should().Be(IpcErrorCode.UnknownGame);

        agent.Answer = static _ => Ack(IpcMessageType.HookEnabledAck, new HookEnabledAck(7, false, "Written", null));
        (await consent.DisableAsync(7, Ct)).Outcome.Should().Be(HookingConsentOutcome.Disabled);
        agent.Sent.Last().Payload.Should().Be(new SetHookEnabledRequest(7, false, null), "a revoke names no disclosure");
    }

    [Theory]
    [InlineData("I ACCEPT THE INJECTION RISK", true)]
    [InlineData("  I ACCEPT THE INJECTION RISK  ", true)]
    [InlineData("i accept the injection risk", false)]
    [InlineData("I ACCEPT THE INJECTION RISK.", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TheAcknowledgementIsThePhraseExactly(string? typed, bool acknowledged)
    {
        System.Globalization.CultureInfo? previous = Shared.Strings.Culture;
        try
        {
            Shared.Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en");
            ConsentDialogViewModel.IsTheAcknowledgement(typed).Should().Be(acknowledged);
            var vm = new ConsentDialogViewModel("Title") { Typed = typed ?? string.Empty };
            vm.IsAcknowledged.Should().Be(acknowledged);
            vm.Title.Should().Be("Enable hooking for Title?");
        }
        finally
        {
            Shared.Strings.Culture = previous;
        }
    }
}

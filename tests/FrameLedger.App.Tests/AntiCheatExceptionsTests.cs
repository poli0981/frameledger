using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;

namespace FrameLedger.App.Tests;

/// <summary>
/// D33 (owner decision 2026-09-26) from the App's side: the version handshake gates the disclosure, a declined disclosure
/// sends nothing, an accepted one sends exactly <c>SetAntiCheatException { enabled: true, disclosureVersion: ours }</c>, and the
/// Agent's answers — granted, refused, option off, version mismatch — come back as what they are.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class AntiCheatExceptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AntiCheatExceptionFacts _gf2 = new("GIRLS' FRONTLINE 2: EXILIUM", "NetEase Yidun", "NEP2.dll", 3);

    private static HelloAck Hello(string? exceptionDisclosure) =>
        new("agent", IpcProtocol.Version, 1, false, "b", false, "l1", false, SafetyDisclosure.Version, exceptionDisclosure);

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

    private sealed class FakePrompt : IAntiCheatExceptionPrompt
    {
        public bool Accepts { get; set; }

        public List<AntiCheatExceptionFacts> Shown { get; } = [];

        public Task<bool> ShowAsync(AntiCheatExceptionFacts facts, CancellationToken ct = default)
        {
            Shown.Add(facts);
            return Task.FromResult(Accepts);
        }
    }

    private static IpcEnvelope Ack<T>(string type, T payload) where T : class => IpcCodec.Decode(IpcCodec.Encode(type, "1", payload));

    [Fact]
    public async Task AnAgentThatGrantsAgainstOtherTextGetsNoDialogAndNoRequest()
    {
        var agent = new FakeAgent { Hello = Hello("ac-exception-dialog/0") };
        var prompt = new FakePrompt { Accepts = true };

        AntiCheatExceptionResult result = await new AntiCheatExceptions(agent, prompt).GrantAsync(56, _gf2, Ct);

        result.Outcome.Should().Be(AntiCheatExceptionOutcome.VersionMismatch);
        prompt.Shown.Should().BeEmpty();
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ADeclinedDisclosureSendsNothing()
    {
        var agent = new FakeAgent { Hello = Hello(AntiCheatExceptionDisclosure.Version) };
        var prompt = new FakePrompt { Accepts = false };

        (await new AntiCheatExceptions(agent, prompt).GrantAsync(56, _gf2, Ct)).Outcome.Should().Be(AntiCheatExceptionOutcome.Declined);

        prompt.Shown.Should().Equal(_gf2);
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAcceptedDisclosureSendsOurVersionAndAGrantIsAGrant()
    {
        var agent = new FakeAgent
        {
            Hello = Hello(AntiCheatExceptionDisclosure.Version),
            Answer = static _ => Ack(IpcMessageType.AntiCheatExceptionAck, new AntiCheatExceptionAck(56, true, "Written", "NetEase Yidun")),
        };

        AntiCheatExceptionResult result = await new AntiCheatExceptions(agent, new FakePrompt { Accepts = true }).GrantAsync(56, _gf2, Ct);

        result.Outcome.Should().Be(AntiCheatExceptionOutcome.Granted);
        agent.Sent.Should().ContainSingle().Which.Should().Be((IpcMessageType.SetAntiCheatException,
            (object)new SetAntiCheatExceptionRequest(56, true, AntiCheatExceptionDisclosure.Version)));
    }

    [Fact]
    public async Task TheAgentsRefusalsComeBackAsWhatTheyAre()
    {
        var agent = new FakeAgent
        {
            Hello = Hello(AntiCheatExceptionDisclosure.Version),
            Answer = static _ => Ack(IpcMessageType.Refused, new RefusedAck(56, "TooFewSessions", "NetEase Yidun", "1")),
        };
        var exceptions = new AntiCheatExceptions(agent, new FakePrompt { Accepts = true });

        AntiCheatExceptionResult few = await exceptions.GrantAsync(56, _gf2, Ct);
        few.Outcome.Should().Be(AntiCheatExceptionOutcome.Refused);
        few.RefusalText().Should().Contain("1");

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.ExceptionsOff, "off");
        (await exceptions.GrantAsync(56, _gf2, Ct)).Outcome.Should().Be(AntiCheatExceptionOutcome.OptionOff);

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.DisclosureVersionMismatch, "restart");
        (await exceptions.GrantAsync(56, _gf2, Ct)).Outcome.Should().Be(AntiCheatExceptionOutcome.VersionMismatch);
    }

    [Fact]
    public async Task AWithdrawalNeedsNoDialog()
    {
        var agent = new FakeAgent
        {
            Hello = Hello(AntiCheatExceptionDisclosure.Version),
            Answer = static _ => Ack(IpcMessageType.AntiCheatExceptionAck, new AntiCheatExceptionAck(56, false, "Written", "NetEase Yidun")),
        };
        var prompt = new FakePrompt();

        (await new AntiCheatExceptions(agent, prompt).WithdrawAsync(56, Ct)).Outcome.Should().Be(AntiCheatExceptionOutcome.Withdrawn);

        prompt.Shown.Should().BeEmpty();
        agent.Sent.Should().ContainSingle().Which.Payload.Should().Be(new SetAntiCheatExceptionRequest(56, false, null));
    }

    [Fact]
    public void TheDisclosuresButtonWaitsForTheTick()
    {
        var viewModel = new AntiCheatExceptionDialogViewModel(_gf2);

        viewModel.Accepted.Should().BeFalse("the dialog opens with the risk unaccepted");
        viewModel.Intro.Should().Contain("NetEase Yidun").And.Contain("NEP2.dll");
        viewModel.Why.Should().Contain("3");
        viewModel.Risk.Should().Contain("NetEase Yidun");
        viewModel.Accepted = true;
        viewModel.Accepted.Should().BeTrue();
    }
}

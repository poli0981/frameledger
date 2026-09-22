using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// The App's one list of running sessions (2026-09-23): the Agent's status on every (re)connect, then the start and
/// completion events; nothing while the Agent is away.
/// </summary>
public sealed class LiveSessionsTests
{
    private static ActiveSession Active(Guid guid, int tier, string name, SessionHold? hold = null) => new(guid, 7, name, 42, tier, DateTimeOffset.UnixEpoch, hold);

    [Fact]
    public void TheStatusSeedsItTheEventsMoveItAndALostAgentEmptiesIt()
    {
        var held = Guid.NewGuid();
        var link = new FakeAgentLink { Status = new StatusAck("recording", null, null, [Active(held, 2, "Held", new SessionHold("TargetUnreadable"))]) };
        using var sessions = new LiveSessions(link);
        int changes = 0;
        sessions.Changed += (_, _) => changes++;

        sessions.Current.Should().ContainSingle()
            .Which.Should().Be(new RunningSession(held, 7, "Held", 2, DateTimeOffset.UnixEpoch, new SessionHold("TargetUnreadable")), "a session that began before the App is there from the start");

        var hooked = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(hooked, 8, "Hooked", 43, 1, DateTimeOffset.UnixEpoch.AddSeconds(5)));
        sessions.Current.Select(static s => s.GameName).Should().Equal("Held", "Hooked");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(held, 3, "normal", 2, "saved", "TargetUnreadable"));
        sessions.Current.Should().ContainSingle().Which.SessionGuid.Should().Be(hooked);

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), 4, "normal", 1, "saved", "TargetExited"));
        changes.Should().Be(2, "a completion for a session not in the list changes nothing");

        link.State = AgentConnectionState.Offline;
        link.RaiseChanged();
        sessions.Current.Should().BeEmpty("a session the Agent can no longer report is not shown as running");

        link.State = AgentConnectionState.Connected;
        link.Status = new StatusAck("capturing", null, 1, [Active(hooked, 1, "Hooked")]);
        link.RaiseChanged();
        sessions.Current.Should().ContainSingle().Which.Tier.Should().Be(1, "the reconnect's status is the truth");
    }
}

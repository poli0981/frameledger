using System.Text;
using System.Text.Json;
using FluentAssertions;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Infrastructure.Tests.Ipc;

/// <summary>
/// The envelope and its payloads through the source-generated context (<c>07_IPC</c> §C, §Versioning):
/// camelCase on the wire, nulls omitted, and — the property everything else rests on — unknown fields
/// ignored in BOTH directions, so an additive field never breaks the older side.
/// </summary>
public sealed class IpcCodecTests
{
    private static readonly HelloAck _ack = new("0.1.0", IpcProtocol.Version, 4242, Elevated: false, OverlayBuildId: null, VulkanLayerRegistered: false, "l1+lhm+nvapi", CpuTempAvailable: false);

    [Fact]
    public void AnAckRoundTripsWithItsIdAndCamelCaseNames()
    {
        byte[] bytes = IpcCodec.Encode(IpcMessageType.HelloAck, "7", _ack);
        string json = Encoding.UTF8.GetString(bytes);

        json.Should().Contain("\"type\":\"HelloAck\"").And.Contain("\"id\":\"7\"").And.Contain("\"agentVersion\":\"0.1.0\"");
        json.Should().NotContain("overlayBuildId", "a null is omitted, not written as null");
        json.Should().NotContain("AgentVersion", "camelCase on the wire");

        IpcEnvelope envelope = IpcCodec.Decode(bytes);
        envelope.Type.Should().Be(IpcMessageType.HelloAck);
        envelope.Id.Should().Be("7");
        envelope.IsEvent.Should().BeFalse();
        IpcCodec.Payload<HelloAck>(envelope).Should().Be(_ack);
    }

    [Fact]
    public void AnEventHasNoIdAndNoIdIsWritten()
    {
        byte[] bytes = IpcCodec.Encode(IpcMessageType.SafetyUnhook, null, new SafetyUnhookEvent(Guid.NewGuid(), "eac", "EasyAntiCheat.dll"));
        Encoding.UTF8.GetString(bytes).Should().NotContain("\"id\"");
        IpcCodec.Decode(bytes).IsEvent.Should().BeTrue();
    }

    [Fact]
    public void AnUnknownTopLevelFieldIsIgnored()
    {
        const string json = "{\"type\":\"Pong\",\"id\":\"3\",\"future\":{\"nested\":true},\"payload\":{}}";
        IpcEnvelope envelope = IpcCodec.Decode(Encoding.UTF8.GetBytes(json));
        envelope.Type.Should().Be(IpcMessageType.Pong);
        envelope.Id.Should().Be("3");
        IpcCodec.Payload<PongAck>(envelope).Should().NotBeNull();
    }

    [Fact]
    public void AnUnknownPayloadFieldIsIgnoredInBothDirections()
    {
        // A newer Agent adds a field: the older client must still read the ack.
        const string newerAck = "{\"type\":\"HelloAck\",\"id\":\"1\",\"payload\":{\"agentVersion\":\"9.9\",\"protocol\":2,\"pid\":1,\"elevated\":false,"
                                + "\"vulkanLayerRegistered\":false,\"cpuTempAvailable\":false,\"gpuName\":\"future\"}}";
        HelloAck? ack = IpcCodec.Payload<HelloAck>(IpcCodec.Decode(Encoding.UTF8.GetBytes(newerAck)));
        ack.Should().NotBeNull();
        ack!.AgentVersion.Should().Be("9.9");
        ack.OverlayBuildId.Should().BeNull("absent is null");

        // A newer client adds a field: the older Agent must still read the request.
        const string newerRequest = "{\"type\":\"Hello\",\"id\":\"2\",\"payload\":{\"appVersion\":\"1.0\",\"protocol\":2,\"locale\":\"vi\"}}";
        HelloRequest? request = IpcCodec.Payload<HelloRequest>(IpcCodec.Decode(Encoding.UTF8.GetBytes(newerRequest)));
        request.Should().Be(new HelloRequest("1.0", 2));
    }

    [Fact]
    public void AnEnvelopeWithoutATypeIsNotAnEnvelope()
    {
        Action decode = () => IpcCodec.Decode(Encoding.UTF8.GetBytes("{\"id\":\"1\"}"));
        decode.Should().Throw<JsonException>();

        Action notJson = () => IpcCodec.Decode(Encoding.UTF8.GetBytes("hello"));
        notJson.Should().Throw<JsonException>();
    }

    [Fact]
    public void AMessageWithoutAPayloadReadsAsNullPayload()
    {
        IpcEnvelope envelope = IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.GetStatus, "5"));
        envelope.Payload.Should().BeNull();
        IpcCodec.Payload<GetStatusRequest>(envelope).Should().BeNull();
    }

    [Fact]
    public void APayloadOfTheWrongShapeIsAJsonExceptionNotASilentDefault()
    {
        const string json = "{\"type\":\"Hello\",\"id\":\"1\",\"payload\":{\"appVersion\":[1,2],\"protocol\":\"two\"}}";
        Action read = () => IpcCodec.Payload<HelloRequest>(IpcCodec.Decode(Encoding.UTF8.GetBytes(json)));
        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void TheProgressEventOmitsEveryUnmeasuredField()
    {
        var progress = new SessionProgressEvent
        {
            SessionGuid = Guid.NewGuid(),
            ElapsedS = 3.5,
            Presents5s = 0,
            PresentedQualifier = "census_not_run",
            FgMode = "na",
        };
        string json = Encoding.UTF8.GetString(IpcCodec.Encode(IpcMessageType.SessionProgress, null, progress));
        json.Should().NotContain("nativeFps5s").And.NotContain("upscaler").And.NotContain("gpuTempC")
            .And.Contain("\"presentedQualifier\":\"census_not_run\"").And.Contain("\"fgMode\":\"na\"");
    }

    /// <summary>
    /// The held session's fields (2026-09-23) are additive, so the protocol stays 2: a held session's tick carries no
    /// measured field at all, its <c>hold</c> rides only on a Tier-2 start, and a beta.5 payload without either still reads.
    /// </summary>
    [Fact]
    public void TheHeldSessionsFieldsAreAdditiveAndCarryNothingMeasured()
    {
        var guid = Guid.NewGuid();
        string held = Encoding.UTF8.GetString(IpcCodec.Encode(IpcMessageType.SessionHeld, null, new SessionHeldEvent(guid, 12.5, GpuTempC: 61)));
        held.Should().Contain("\"elapsedS\":12.5").And.Contain("\"gpuTempC\":61")
            .And.NotContain("cpuTempC").And.NotContain("fps", "a held session has nothing measured");

        var started = new SessionStartedEvent(guid, 3, "Title", 42, 2, DateTimeOffset.UnixEpoch, new SessionHold("RefusedByGuard", "BlockedModule", "eac", "EasyAntiCheat.dll", HookingTurnedOff: true));
        IpcCodec.Payload<SessionStartedEvent>(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.SessionStarted, null, started))).Should().Be(started);
        string hooked = Encoding.UTF8.GetString(IpcCodec.Encode(IpcMessageType.SessionStarted, null, started with { Tier = 1, Hold = null }));
        hooked.Should().NotContain("hold", "a hooked session has no hold and says nothing about one");

        const string beta5Start = "{\"type\":\"SessionStarted\",\"payload\":{\"sessionGuid\":\"6ae6087f-0000-0000-0000-000000000000\",\"gameId\":3,"
                                  + "\"gameName\":\"Title\",\"pid\":42,\"tier\":1,\"startedAt\":\"2026-09-22T00:00:00+00:00\"}}";
        IpcCodec.Payload<SessionStartedEvent>(IpcCodec.Decode(Encoding.UTF8.GetBytes(beta5Start)))!.Hold.Should().BeNull();

        const string beta5Done = "{\"type\":\"SessionCompleted\",\"payload\":{\"sessionGuid\":\"6ae6087f-0000-0000-0000-000000000000\",\"sessionId\":5,"
                                 + "\"exitStatus\":\"normal\",\"tier\":1,\"finalize\":\"saved\",\"reason\":\"TargetExited\"}}";
        SessionCompletedEvent done = IpcCodec.Payload<SessionCompletedEvent>(IpcCodec.Decode(Encoding.UTF8.GetBytes(beta5Done)))!;
        done.GameId.Should().BeNull();
        done.GameName.Should().BeNull();

        var status = new StatusAck("recording", null, null, [new ActiveSession(guid, 3, "Title", 42, 2, DateTimeOffset.UnixEpoch, new SessionHold("RefusedHookNotEnabled"))]);
        IpcCodec.Payload<StatusAck>(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.StatusAck, "1", status)))!.ActiveSessions.Single().Hold
            .Should().Be(new SessionHold("RefusedHookNotEnabled"));
    }
}

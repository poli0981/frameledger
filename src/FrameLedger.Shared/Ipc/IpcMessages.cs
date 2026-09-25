// The message set of 07_IPC §Messages: the READ half (P3 PR-1, 2026-09-13: Hello/GetStatus/Ping and the
// Agent → UI events) and the COMMAND half (P3 PR-1b, the same day: SetWatchlist, LaunchGame, SetHookEnabled,
// Pause/Resume, StopSession, UpdateRules, Shutdown). Every type here has a handler in AgentRequestHandler or
// AgentCommandHandler — a declared message nobody answers is the §S29(c) shape.
//
// ONE FILE FOR THE WHOLE SET, deliberately (MA0048 is suppressed for this project, see the csproj): the types
// are one wire contract read against one table in 07_IPC, and the property names ARE the JSON — camelCase by the
// context's naming policy, nulls omitted. Rename a property and the other process stops understanding it.
//
// Every value that is not measured is null, never 0 (FR-4.9 at the wire).

namespace FrameLedger.Shared.Ipc;

/// <summary>The <c>type</c> discriminators, one per row of <c>07_IPC</c> §Messages.</summary>
public static class IpcMessageType
{
    public const string Hello = "Hello";
    public const string HelloAck = "HelloAck";
    public const string GetStatus = "GetStatus";
    public const string StatusAck = "StatusAck";
    public const string Ping = "Ping";
    public const string Pong = "Pong";

    /// <summary>The Agent's answer when it cannot answer: unknown type, malformed payload, protocol mismatch.</summary>
    public const string Error = "Error";

    public const string SetWatchlist = "SetWatchlist";
    public const string WatchlistAck = "WatchlistAck";
    public const string LaunchGame = "LaunchGame";
    public const string LaunchAck = "LaunchAck";
    public const string SetHookEnabled = "SetHookEnabled";

    public const string HookEnabledAck = "HookEnabledAck";

    /// <summary>The Agent's answer to <c>SetHookEnabled</c> when its own pre-scan said no (07_IPC: "may reply Refused").</summary>
    public const string Refused = "Refused";

    public const string PauseCapture = "PauseCapture";
    public const string ResumeCapture = "ResumeCapture";
    public const string PauseAck = "PauseAck";
    public const string StopSession = "StopSession";
    public const string StopAck = "StopAck";
    public const string UpdateRules = "UpdateRules";
    public const string UpdateRulesAck = "UpdateRulesAck";
    public const string Shutdown = "Shutdown";
    public const string ShutdownAck = "ShutdownAck";

    /// <summary>Tools ▸ Database maintenance (P4 PR-7): the on-demand retention sweep, which the Agent runs because the blob tables are its.</summary>
    public const string SweepRetention = "SweepRetention";
    public const string SweepRetentionAck = "SweepRetentionAck";

    public const string SessionStarted = "SessionStarted";
    public const string SessionProgress = "SessionProgress";

    /// <summary>1 Hz while a session runs UNHOOKED (Tier 2) and a client listens (2026-09-23): elapsed and telemetry, nothing measured.</summary>
    public const string SessionHeld = "SessionHeld";
    public const string SessionCompleted = "SessionCompleted";
    public const string CaptureRefused = "CaptureRefused";
    public const string CaptureDegraded = "CaptureDegraded";
    public const string SafetyUnhook = "SafetyUnhook";
    public const string CaptureError = "CaptureError";

    /// <summary>
    /// The Agent's pre-scan of the library turned an entry's hooking OFF (2026-09-25): the user had turned it on, and a rules
    /// update or a game update since has put anti-cheat in its folder or on its title lists. Not a session event — no game
    /// need be running.
    /// </summary>
    public const string HookingTurnedOff = "HookingTurnedOff";
}

/// <summary>Codes an <see cref="ErrorAck"/> carries.</summary>
public static class IpcErrorCode
{
    public const string UnknownType = "UnknownType";
    public const string Malformed = "Malformed";
    public const string ProtocolMismatch = "ProtocolMismatch";
    public const string HandlerFaulted = "HandlerFaulted";

    /// <summary><c>SetHookEnabled true</c> passed the pre-scan and was NOT stamped: this Agent carries no reviewed disclosure (a build without <c>Shared.Safety.SafetyDisclosure</c> wired — the unshipped host's composition, never <c>--serve</c>).</summary>
    public const string DisclosureUnavailable = "DisclosureUnavailable";

    /// <summary><c>SetHookEnabled true</c> named a disclosure version other than the Agent's own: nothing stamped, restart both (D14).</summary>
    public const string DisclosureVersionMismatch = "DisclosureVersionMismatch";

    public const string UnknownGame = "UnknownGame";

    public const string ExecutableUnreadable = "ExecutableUnreadable";
}

/// <summary>Codes a <see cref="CaptureErrorEvent"/> carries (<c>07_IPC</c> §Messages, <c>CaptureError</c>).</summary>
public static class CaptureErrorCode
{
    public const string InjectFailed = "InjectFailed";
    public const string RingVersionMismatch = "RingVersionMismatch";
    public const string AttachRefused = "AttachRefused";
    public const string TelemetryUnavailable = "TelemetryUnavailable";
    public const string DbWriteFailed = "DbWriteFailed";

    /// <summary>The session's task threw before it could finalize; the <c>.partial</c> stays for recovery.</summary>
    public const string SessionFaulted = "SessionFaulted";
}

/// <summary><c>Hello</c>: the client names itself and the protocol it speaks.</summary>
public sealed record HelloRequest(string AppVersion, int Protocol);

/// <summary>
/// <c>HelloAck</c>: what this Agent is. <c>etwAvailable</c> from the original table is gone with the ETW tier
/// (2026-08-28); <c>VulkanLayerRegistered</c> is false by construction today — the layer is handed to a launched
/// process through <c>VK_ADD_IMPLICIT_LAYER_PATH</c>, never registered machine-wide (P1 item 3).
/// <c>DisclosureVersion</c> (P3 PR-4, D14) is the FR-2.1 text this Agent stamps against; the App refuses to open
/// the consent dialog when it differs from its own <c>SafetyDisclosure.Version</c>.
/// </summary>
public sealed record HelloAck(
    string AgentVersion,
    int Protocol,
    int Pid,
    bool Elevated,
    string? OverlayBuildId,
    bool VulkanLayerRegistered,
    string? TelemetrySource,
    bool CpuTempAvailable,
    string? DisclosureVersion = null);

/// <summary><c>GetStatus</c>: no payload.</summary>
public sealed record GetStatusRequest;

/// <summary>
/// Why a session runs without being measured (2026-09-23): the words its <c>capture_notes</c> will carry — the loop's end
/// reason and, when the guard said it, the guard's reason, family and signal — so a live card can say what the summary
/// will. Sent with a Tier-2 <see cref="SessionStartedEvent"/> and on its <see cref="ActiveSession"/>; absent for Tier 1.
/// </summary>
public sealed record SessionHold(string Reason, string? GuardReason = null, string? Family = null, string? Signal = null, bool HookingTurnedOff = false);

/// <summary>
/// One session the Agent is running right now. Tier 1 once the ring is attached, 2 until then or never; <see cref="Hold"/>
/// says why a Tier-2 session measures nothing (2026-09-23, optional).
/// </summary>
public sealed record ActiveSession(Guid SessionGuid, long GameId, string? GameName, int Pid, int Tier, DateTimeOffset StartedAt, SessionHold? Hold = null);

/// <summary>
/// <c>StatusAck</c>. <see cref="State"/> is <c>idle</c>, <c>recording</c> (a session runs, nothing hooked) or
/// <c>capturing</c> (at least one ring attached); <see cref="ActiveSession"/> is the hooked one when there is one.
/// </summary>
public sealed record StatusAck(string State, ActiveSession? ActiveSession, int? Tier, IReadOnlyList<ActiveSession> ActiveSessions, bool Paused = false);

public sealed record PingRequest;

public sealed record PongAck;

public sealed record ErrorAck(string Code, string Message);

/// <summary>
/// Published ONCE per session: when the ring is attached (Tier 1), or — since 2026-09-23 — when a session that will not
/// be hooked starts its hold (Tier 2, with <see cref="Hold"/>). Until then an unhooked session had no start event at all,
/// and the live card read "Nothing is being captured." for the whole of a hooking-off game.
/// </summary>
public sealed record SessionStartedEvent(Guid SessionGuid, long GameId, string? GameName, int Pid, int Tier, DateTimeOffset StartedAt, SessionHold? Hold = null);

/// <summary>
/// 1 Hz while a Tier-2 session is held and a client is connected (2026-09-23): how long it has run and the machine's
/// telemetry. Never a frame-derived value — there is none (CLAUDE.md rule 6 has nothing to show, and shows nothing).
/// Its own type rather than <see cref="SessionProgressEvent"/>, whose required fields would put measured-looking zeros on the wire.
/// </summary>
public sealed record SessionHeldEvent(Guid SessionGuid, double ElapsedS, double? GpuTempC = null, double? CpuTempC = null, double? GpuLoadPct = null, double? CpuLoadPct = null);

/// <summary>
/// 1 Hz while a hooked session runs and a client is connected (<c>04_CAPTURE</c> §Live progress). A 5 s rolling
/// window over the ring's records, computed by the same Domain calculators the stored row is — never a second
/// implementation of a metric.
/// </summary>
/// <remarks>
/// CLAUDE.md rule 6 at the wire: <see cref="NativeFps5s"/>, <see cref="DisplayedFps5s"/> and <see cref="FgFactor"/>
/// are set only when frame generation was MEASURED in the window; otherwise the one number that may stand alone is
/// <see cref="PresentedFps5s"/>, and <see cref="PresentedQualifier"/> is mandatory beside it.
/// </remarks>
public sealed record SessionProgressEvent
{
    public required Guid SessionGuid { get; init; }

    /// <summary>Seconds since the ring was attached.</summary>
    public required double ElapsedS { get; init; }

    /// <summary>Presents in the window.</summary>
    public required int Presents5s { get; init; }

    public double? PresentedFps5s { get; init; }

    /// <summary><c>census_not_run</c> · <c>no_fg_runtime</c> · <c>fg_runtime_loaded</c> · <c>none_withheld</c>.</summary>
    public required string PresentedQualifier { get; init; }

    public double? NativeFps5s { get; init; }

    public double? DisplayedFps5s { get; init; }

    public double? FgFactor { get; init; }

    /// <summary>The row's vocabulary: <c>na</c>, <c>none</c>, <c>dlssg</c>, <c>fsrfg</c>, <c>xefg</c>, <c>unknown</c>.</summary>
    public required string FgMode { get; init; }

    /// <summary>
    /// The row's <c>fg_refusal</c> (schema 0003): why <see cref="FgFactor"/> is absent while <see cref="FgMode"/> names a
    /// technology — <c>no_evaluations</c>, <c>non_uniform</c>, … Absent when a factor stands. Added 2026-09-14; an older
    /// client ignores it (<c>07_IPC</c> §C, unknown fields).
    /// </summary>
    public string? FgRefusal { get; init; }

    /// <summary>
    /// <c>FlWriterState.runtimeCensus</c>, raw, so the live card can name the frame-generation module behind
    /// <c>fg_runtime_loaded</c> the way the stored row's <c>fg_runtime_census</c> lets the summary. Absent before the
    /// first record. Added 2026-09-14.
    /// </summary>
    public long? FgRuntimeCensus { get; init; }

    public string? Upscaler { get; init; }

    /// <summary>The vendor's own preset value, as the row stores it; null when no record carried params.</summary>
    public string? UpscalerQuality { get; init; }

    /// <summary>
    /// <c>dlss</c> when the NVIDIA driver reports an NGX super-resolution feature created and evaluated in the target
    /// (<c>03_METRICS</c> §The driver-reported rung), else null. The row has carried it since P2; the live card did not
    /// until 2026-09-21, so an NGX-direct title read "Unknown upscaler" for the whole session. Identity only.
    /// </summary>
    public string? UpscalerDriverReported { get; init; }

    public int? RenderW { get; init; }

    public int? RenderH { get; init; }

    public int? OutputW { get; init; }

    public int? OutputH { get; init; }

    /// <summary>Null when no record in the window claimed a ray-tracing measurement.</summary>
    public bool? RtActive { get; init; }

    public double? GpuTempC { get; init; }

    /// <summary>Null unless the Agent is elevated with PawnIO installed: no unprivileged API reads a CPU's thermal sensor.</summary>
    public double? CpuTempC { get; init; }

    /// <summary>CPU time busy across every logical processor over the last telemetry tick, 0-100 (2026-09-21, optional). Null before the second tick.</summary>
    public double? CpuLoadPct { get; init; }

    public int? VramProcMb { get; init; }

    public int? LatencyUs { get; init; }
}

/// <summary>
/// The session is over and, when <see cref="SessionId"/> is set, in SQLite; the UI loads the row from there
/// (<c>07_IPC</c> §Client behavior). <see cref="Finalize"/> is <c>saved</c>, <c>discarded</c>, <c>game_removed</c> or
/// <c>faulted</c>. <see cref="GameId"/> and <see cref="GameName"/> (2026-09-23, optional) name the entry it belongs to, so a
/// notice about it never borrows another session's name.
/// </summary>
public sealed record SessionCompletedEvent(Guid SessionGuid, long? SessionId, string ExitStatus, int Tier, string Finalize, string Reason, long? GameId = null, string? GameName = null);

/// <summary>The gate or the guard said no before anything was injected (<c>08_UI</c> §Safety events: never a toast).</summary>
public sealed record CaptureRefusedEvent(long GameId, string? GameName, string Reason, string? Family, string? Signal, bool HookingTurnedOff = false);

/// <summary>Measurement STOPPED mid-session (two-rung ladder: there is no lower fidelity to continue at).</summary>
public sealed record CaptureDegradedEvent(Guid SessionGuid, int From, int To, string Reason);

/// <summary>Anti-cheat appeared mid-session and our own guard published the stop.</summary>
public sealed record SafetyUnhookEvent(Guid SessionGuid, string? Family, string? Signal, bool HookingTurnedOff = false);

public sealed record CaptureErrorEvent(Guid? SessionGuid, string Code, string Message);

/// <summary><see cref="IpcMessageType.HookingTurnedOff"/>: which entry, and what the guard found (its reason, family and signal).</summary>
public sealed record HookingTurnedOffEvent(long GameId, string? GameName, string Reason, string? Family, string? Signal);

// ----------------------------------------------------------------------------------------------------------------
// The command half (P3 PR-1b). 07_IPC §The pipe is not a trust boundary: none of these carries a verdict, a
// clearance, a consent record or a rules source — a client asks, the Agent establishes the fact itself.
// ----------------------------------------------------------------------------------------------------------------

/// <summary>One watchlist entry: identity only. <c>GameId</c> is the client's hint and is never trusted over the path.</summary>
public sealed record WatchlistEntry(long? GameId, string ExePath);

/// <summary><c>SetWatchlist</c>: ensure a <c>games</c> row per entry (hooking OFF when new). Removal is FR-1.4's, not this message's.</summary>
public sealed record SetWatchlistRequest(IReadOnlyList<WatchlistEntry> Entries);

public sealed record WatchlistGame(long GameId, string ExePath, string Name);

public sealed record WatchlistAck(IReadOnlyList<WatchlistGame> Games, IReadOnlyList<string> Unreadable);

/// <summary><c>LaunchGame</c>: start the title in launch mode (<c>04_CAPTURE</c> §Launch mode) and run the launcher election after it.</summary>
public sealed record LaunchGameRequest(long GameId, string? Arguments);

public sealed record LaunchAck(long GameId, bool Accepted, string Outcome, Guid? SessionGuid);

/// <summary>
/// <c>SetHookEnabled</c>. <c>DisclosureVersion</c> is the version of the reviewed FR-2.1 text the client showed; the
/// Agent compares it to its own and stamps from its own clock (built P3 PR-4: <c>ConsentProvenance.ConsentDialog</c>,
/// or <c>Error DisclosureVersionMismatch</c>). It is never a consent timestamp.
/// </summary>
public sealed record SetHookEnabledRequest(long GameId, bool Enabled, string? DisclosureVersion);

/// <summary><c>Outcome</c> is the store's word (<c>Written</c>, <c>NotFound</c>, …); <c>Prescan</c> is <c>clean</c> when one ran and passed.</summary>
public sealed record HookEnabledAck(long GameId, bool Enabled, string Outcome, string? Prescan);

/// <summary>The Agent's pre-scan refused to enable: the reason, and the family/signal when it named one. The block is on the row.</summary>
public sealed record RefusedAck(long GameId, string Reason, string? Family, string? Signal);

public sealed record PauseCaptureRequest;

public sealed record ResumeCaptureRequest;

public sealed record PauseAck(bool Paused);

public sealed record StopSessionRequest(Guid SessionGuid);

public sealed record StopAck(Guid SessionGuid, bool Accepted, string? Reason);

/// <summary><c>UpdateRules</c>: a trigger, no payload — the Agent re-reads only its own rules copy (07_IPC).</summary>
public sealed record UpdateRulesRequest;

public sealed record UpdateRulesAck(string Outcome);

public sealed record ShutdownRequest;

public sealed record ShutdownAck;

/// <summary>
/// <c>SweepRetention</c> (P4 PR-7): a trigger, no payload. The Agent applies <c>06_DATA_MODEL</c> §Retention to every
/// game with the <c>retention.raw_sessions_per_game</c> it reads itself — the client names no number, because the
/// rows it would delete are the Agent's (§Writer ownership).
/// </summary>
public sealed record SweepRetentionRequest;

/// <summary>
/// What the sweep did: <see cref="Keep"/> is the setting it applied (0 = unlimited, and then nothing was swept),
/// <see cref="Games"/> the games that have sessions, <see cref="Sessions"/> the sessions whose raw frame or sensor
/// series were removed. Aggregates and segments are never touched.
/// </summary>
public sealed record SweepRetentionAck(int Keep, int Games, int Sessions);

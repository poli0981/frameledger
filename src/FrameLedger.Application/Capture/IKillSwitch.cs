namespace FrameLedger.Application.Capture;

/// <summary>
/// FR-2.4's global "disable all hooking" switch, as the capture path reads it (P2 PR-F, HANDOFF §P2 decision D7).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the FOURTH input of <c>HookedCaptureGate</c>, not a check upstream of it.</b> The gate's own remark
/// calls itself "the ONLY managed logic between the user's intent and the guard", and a global switch is intent;
/// enforcing it somewhere earlier would make that sentence false in the one place a reader would go to check
/// it. <c>CaptureSession</c> reads the switch once before it builds the <c>HookRequest</c>, and once more at
/// every guard-scan boundary — mid-session, an engaged switch is published as <c>unhookRequested</c>, the same
/// signal a safety refusal sends, so the Overlay stops the way it already knows how to.
/// </para>
/// <para>
/// The Vulkan layer honours it through the launcher: an engaged switch means the Agent does <b>not</b> set
/// <c>FRAMELEDGER_ENABLE_VK_LAYER</c> when it starts a title, and the loader never maps the layer
/// (<c>17_HOOK_ENGINE</c> §Vulkan — the variable's value is compared, so this is the only honest way to say no).
/// </para>
/// <para>
/// Read fresh on every ask rather than cached: the switch is one row in <c>settings</c>, the UI (P3) flips it,
/// and "must take effect immediately" (<c>08_UI</c> §Settings) is a property of the reader, not the writer.
/// </para>
/// </remarks>
public interface IKillSwitch
{
    /// <summary>True while the global switch is on and nothing may be injected.</summary>
    ValueTask<bool> IsEngagedAsync(CancellationToken ct = default);
}

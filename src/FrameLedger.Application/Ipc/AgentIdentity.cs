namespace FrameLedger.Application.Ipc;

/// <summary>
/// What <c>HelloAck</c> says about this Agent, established once by the composition root — every field is a
/// fact about the process, not a claim about a capture.
/// </summary>
/// <param name="AgentVersion">The assembly's informational version.</param>
/// <param name="Pid">This process.</param>
/// <param name="Elevated">Whether the process is privileged; no capture tier needs it (ADR-9).</param>
/// <param name="OverlayBuildId">The guard's build id, which the Overlay shares; null when the native side did not answer.</param>
/// <param name="VulkanLayerRegistered">False by construction: the layer is per-launch, never machine-wide (P1 item 3).</param>
/// <param name="CpuTempAvailable">False until the Agent composes a CPU sensor (LHM's CPU half is off unelevated).</param>
/// <param name="DisclosureVersion">The FR-2.1 disclosure this Agent stamps against (<c>Shared.Safety.SafetyDisclosure.Version</c> under <c>--serve</c>; null on a composition that carries none) — D14, P3 PR-4.</param>
public sealed record AgentIdentity(
    string AgentVersion,
    int Pid,
    bool Elevated,
    string? OverlayBuildId,
    bool VulkanLayerRegistered,
    bool CpuTempAvailable,
    string? DisclosureVersion = null);

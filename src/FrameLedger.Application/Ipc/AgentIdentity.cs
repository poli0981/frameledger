// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Ipc;

/// <summary>
/// What <c>HelloAck</c> says about this Agent, established once by the composition root — every field is a
/// fact about the process, not a claim about a capture.
/// </summary>
/// <param name="AgentVersion">The assembly's informational version.</param>
/// <param name="Pid">This process.</param>
/// <param name="Elevated">Whether the process is privileged; no capture tier needs it (ADR-9).</param>
/// <param name="OverlayBuildId">The guard's build id, which the Overlay shares; null when the native side did not answer.</param>
/// <param name="VulkanLayerRegistered">Whether the HKCU implicit-layer registration named our manifest at this process's start (P3 PR-8b; a launch never needs it).</param>
/// <param name="CpuTempAvailable">False until the Agent composes a CPU sensor (LHM's CPU half is off unelevated).</param>
/// <param name="DisclosureVersion">The FR-2.1 disclosure this Agent stamps against (<c>Shared.Safety.SafetyDisclosure.Version</c> under <c>--serve</c>; null on a composition that carries none) — D14, P3 PR-4.</param>
/// <param name="ExceptionDisclosureVersion">D33: the user-mode exception's disclosure this Agent grants against (<c>Shared.Safety.AntiCheatExceptionDisclosure.Version</c> under <c>--serve</c>; null where none is carried).</param>
/// <param name="ElevationOutcome">
/// The admin mode's answer at this Agent's start (beta.10, D34; <c>Infrastructure.Startup.AgentElevation</c>): <c>granted</c>,
/// <c>declined</c>, <c>other-account</c> or <c>failed</c>; null when nobody asked — the option off, or elevation inherited.
/// </param>
public sealed record AgentIdentity(
    string AgentVersion,
    int Pid,
    bool Elevated,
    string? OverlayBuildId,
    bool VulkanLayerRegistered,
    bool CpuTempAvailable,
    string? DisclosureVersion = null,
    string? ExceptionDisclosureVersion = null,
    string? ElevationOutcome = null);

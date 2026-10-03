// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;
using FrameLedger.Application.Capture;
using FrameLedger.Infrastructure.Io;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Infrastructure.Vulkan;

namespace FrameLedger.Infrastructure.Capture;

/// <summary>
/// Launch mode's first step: start the consented executable and hold it from birth.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <c>CREATE_SUSPENDED</c>, and the spec's sentence is why.</b> <c>04_CAPTURE</c> §Launch mode
/// wrote <i>create suspended → guard → inject → resume</i>, and <c>20_OPEN_QUESTIONS</c> §S1 measured
/// that a suspended target has loaded nothing: <c>EnumProcessModulesEx</c> fails against it, so the guard
/// cannot run before the loader has. The built shape is <i>create → guard WAITS for a presentation
/// runtime → inject</i> (<c>FlGuardedInjectWhenReady</c>), and nothing in it needs the process held at
/// its first instruction. What the launch buys instead: the pid is ours before any code runs, the handle
/// is held from the start so it cannot recycle under us (§S29(e)), and the Vulkan layer's
/// <c>enable_environment</c> is set — which only the launching process can do (<c>17_HOOK_ENGINE</c>
/// §Vulkan). The layer still gates on its own enabled-list; the variable alone enables nothing.
/// </para>
/// <para>
/// <b>This host never terminates what it launched.</b> A refusal after the launch — no consent, an
/// anti-cheat hit, no runtime inside the budget — leaves the title running unhooked, exactly as the
/// product's Tier 2 does (duration and the reason, no measurement). The operator asked for the game to
/// start; the guard decides only whether FrameLedger goes into it.
/// </para>
/// <para>
/// <b>An elevated Agent never starts a game elevated</b> (beta.10, the Agent's admin mode, D34): the game gets the token the
/// desktop shell runs with (<see cref="UnelevatedProcess"/>), which is what a double-click would give it — otherwise
/// <c>LaunchGame</c>, which any process of the same user can send, would be an elevation without a prompt. When no shell
/// token can be had the launch is refused with that reason, never made elevated instead.
/// </para>
/// </remarks>
public sealed class ProcessLauncher : IProcessLauncher
{
    private readonly IReadOnlyDictionary<string, string>? _environment;
    private readonly bool _enableVulkanLayer;

    /// <summary>A launcher whose children get <paramref name="environment"/> on top of this process's.</summary>
    /// <param name="environment">
    /// Variables added to every child's environment — the Vulkan layer's, from
    /// <see cref="VkLayerLaunchEnvironment.Variables"/>, when the layer is staged beside the host.
    /// </param>
    /// <param name="enableVulkanLayer">
    /// Whether the child gets <see cref="VkLayerLaunchEnvironment.EnableVariable"/>. False is how FR-2.4's kill
    /// switch reaches the Vulkan layer (P2 PR-F, decision D7): the loader compares the variable's value, so not
    /// setting it is the only honest "no" — the layer is never mapped, and nothing has to be told to stop.
    /// </param>
    public ProcessLauncher(IReadOnlyDictionary<string, string>? environment = null, bool enableVulkanLayer = true)
    {
        _environment = environment;
        _enableVulkanLayer = enableVulkanLayer;
    }

    /// <inheritdoc />
    public (int Pid, ITargetLiveness Alive)? Start(string exePath, string arguments) => Start(exePath, arguments, _environment, _enableVulkanLayer);

    /// <inheritdoc />
    public (int Pid, ITargetLiveness Alive)? Start(string exePath, string arguments, out int? win32Error) =>
        StartCore(exePath, arguments, _environment, _enableVulkanLayer, out win32Error);

    /// <summary>Start <paramref name="exePath"/> with <paramref name="arguments"/>; null when it could not be started or pinned.</summary>
    /// <param name="exePath">The consented executable.</param>
    /// <param name="arguments">Its own command line, verbatim.</param>
    /// <param name="environment">
    /// Variables added to the child's environment — the Vulkan layer's, from
    /// <c>VkLayerLaunchEnvironment</c>, when the layer is staged beside this host (P1 item 3).
    /// </param>
    /// <param name="enableVulkanLayer">False leaves <see cref="VkLayerLaunchEnvironment.EnableVariable"/> unset (the kill switch, decision D7).</param>
    public static (int Pid, ITargetLiveness Alive)? Start(string exePath, string arguments,
        IReadOnlyDictionary<string, string>? environment = null, bool enableVulkanLayer = true) =>
        StartCore(exePath, arguments, environment, enableVulkanLayer, out _);

    /// <summary>The start, keeping the Win32 error of one that failed (beta.8: it was swallowed, and the user read only "could not start").</summary>
    private static (int Pid, ITargetLiveness Alive)? StartCore(string exePath, string arguments,
        IReadOnlyDictionary<string, string>? environment, bool enableVulkanLayer, out int? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        error = null;

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (enableVulkanLayer)
        {
            variables[VkLayerLaunchEnvironment.EnableVariable] = "1";
        }

        foreach ((string name, string value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            variables[name] = value;
        }

        string workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        if (Environment.IsPrivilegedProcess)
        {
            return StartUnelevated(exePath, arguments ?? string.Empty, workingDirectory, variables, out error);
        }

        var psi = new ProcessStartInfo(exePath, arguments ?? string.Empty)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach ((string name, string value) in variables)
        {
            psi.Environment[name] = value;
        }

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            error = e is Win32Exception w ? w.NativeErrorCode : null;
            return null;
        }

        if (process is null)
        {
            return null;
        }

        using (process)
        {
            return Pin(process.Id);
        }
    }

    /// <summary>The start an elevated Agent makes: the shell's token, and the same pin, taken while the start's own handle still holds the id.</summary>
    private static (int Pid, ITargetLiveness Alive)? StartUnelevated(string exePath, string arguments, string workingDirectory,
        IReadOnlyDictionary<string, string> variables, out int? error)
    {
        error = null;
        (int Pid, IDisposable Process)? started = UnelevatedProcess.Start(exePath, arguments, workingDirectory, variables, out int failure);
        if (started is not { } s)
        {
            error = failure;
            return null;
        }

        using (s.Process)
        {
            return Pin(s.Pid);
        }
    }

    /// <summary>
    /// The same pin attach mode takes, taken before anything else looks at the pid. The liveness OWNS the handle from here; the
    /// caller disposes it with the session (CA2000 is satisfied by that transfer, stated rather than suppressed).
    /// </summary>
    private static (int Pid, ITargetLiveness Alive)? Pin(int pid)
    {
        HeldProcessHandle? held = null;
        try
        {
            held = HeldProcessHandle.TryOpen(pid);
            if (held is null)
            {
                return null;
            }

            ITargetLiveness alive = new ProcessTargetLiveness(held, pid);
            held = null;
            return (pid, alive);
        }
        finally
        {
            held?.Dispose();
        }
    }
}

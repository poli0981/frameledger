using FrameLedger.Domain.AntiCheat;

namespace FrameLedger.Application.AntiCheat;

/// <summary>
/// Runs the in-session guard re-scan and publishes proof that it ran.
/// </summary>
/// <remarks>
/// <para>
/// <c>19_SAFETY</c> §During a session calls this "the single most important
/// runtime behavior in the whole capture layer". The capture side — the
/// injected Overlay, or the Vulkan layer, which cannot scan for itself — decides
/// whether to keep observing by watching <c>FlControlBlock.guardTicks</c>.
/// </para>
/// <para>
/// <b>The tick counts completed evaluations, never seconds.</b> That field was
/// originally specified as "Agent bumps every second", and a timer-driven tick
/// is the wrong signal: it attests that this process is alive while the guard
/// loop can be dead — a swallowed exception, a blocked service query, a stall on
/// one unreadable process in the §S16 scan set. The capture side would then keep
/// observing <i>because</i> the thing supervising it had stopped. This class
/// exists so that cannot happen: the counter advances at exactly one place, on
/// the far side of a returned verdict.
/// </para>
/// </remarks>
public sealed class GuardSupervisor(IAntiCheatGuard guard, bool guardBypassAcknowledged = false)
{
    private readonly IAntiCheatGuard _guard = guard ?? throw new ArgumentNullException(nameof(guard));

    /// <summary>
    /// Completed guard evaluations. Published to <c>FlControlBlock.guardTicks</c>.
    /// </summary>
    public uint CompletedEvaluations { get; private set; }

    /// <summary>
    /// True once a scan has refused. Published to <c>FlControlBlock.unhookRequested</c>.
    /// </summary>
    /// <remarks>
    /// Latches. A later clean scan does not clear it: the session is finalised
    /// <c>unhooked_safety</c> and does not resume, because "anti-cheat was
    /// present a moment ago" is not a state capture should recover from on its
    /// own (<c>19_SAFETY</c> §During a session).
    /// </remarks>
    public bool UnhookRequested { get; private set; }

    /// <summary>The verdict of the most recent completed evaluation, if any.</summary>
    public AntiCheatVerdict? LastVerdict { get; private set; }

    /// <summary>How many in-session scans refused on the guard's judgement and were overruled by the user's bypass.</summary>
    public uint JudgementsOverruled { get; private set; }

    /// <summary>
    /// Run one re-scan. Returns true if capture may continue.
    /// </summary>
    /// <remarks>
    /// Exceptions are deliberately NOT swallowed. If the guard throws, this
    /// method throws, the tick does not advance, and the capture side sees
    /// supervision stop — which is the correct outcome. Catching and continuing
    /// here is precisely how a supervisor comes to look alive while doing
    /// nothing.
    /// </remarks>
    public async ValueTask<bool> ScanOnceAsync(int targetPid, CancellationToken ct = default)
    {
        if (UnhookRequested)
        {
            // Already latched. Do not re-ask: a clean answer now cannot undo it,
            // and asking again would let the tick keep advancing while the
            // session is supposed to be stopping.
            return false;
        }

        AntiCheatVerdict verdict = await _guard.EvaluateAsync(targetPid, ct).ConfigureAwait(false);

        // THE ONE SITE. Everything above can fail, throw or cancel; nothing
        // below runs unless a verdict actually came back.
        LastVerdict = verdict;
        CompletedEvaluations++;

        if (!verdict.IsAllowed)
        {
            // A SESSION THAT STARTED UNDER THE USER'S BYPASS (owner decision 2026-09-21) already knows the guard
            // refuses this target: that refusal is what the user overruled, and re-discovering it every 30 s is not
            // news. The scan still RUNS and the tick still counts — the Overlay's own stop is fed by it — and the
            // verdict is kept, so the log says what was found. Anything that is not the guard's judgement still stops
            // the session: the bypass overrules a judgement, never a fact.
            if (guardBypassAcknowledged && AntiCheatVerdict.IsGuardJudgement(verdict.Reason))
            {
                JudgementsOverruled++;
                return true;
            }

            UnhookRequested = true;
            return false;
        }

        return true;
    }
}

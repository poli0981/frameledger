using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The Agent's first act at startup (<c>04_CAPTURE</c> §Session recorder: <c>Interrupted → recovered from
/// .partial</c>): every pending file becomes an <c>interrupted</c> session from its valid prefix, or is
/// dropped for one of four stated reasons — already stored, too short, unreadable, its game removed. Nothing is
/// retried, nothing is left behind.
/// </summary>
/// <remarks>
/// <b>Recovery never throws (2026-09-23).</b> It runs before the watcher on every start, and the host stops on a
/// background service's exception: one file whose finalize failed — two GIRLS' FRONTLINE 2 sessions whose entry was
/// removed mid-session left exactly that — would have stopped every start from then on, and with it every capture.
/// A file that cannot be finalized is set aside (<see cref="IPartialSessionStore.Quarantine"/>) and reported as
/// <see cref="RecoveryStatus.Failed"/>; the next file is recovered.
/// </remarks>
public sealed class PartialRecovery
{
    private readonly IPartialSessionStore _store;
    private readonly SessionFinalizer _finalizer;
    private readonly TimeSpan _minimumSessionLength;

    /// <summary>Recovery over one store, finalizing through one finalizer.</summary>
    /// <param name="store">Where the pending files are.</param>
    /// <param name="finalizer">What turns a prefix into a row.</param>
    /// <param name="minimumSessionLength">The discard rule's threshold; the Agent keeps the default, the unshipped host lowers it.</param>
    public PartialRecovery(IPartialSessionStore store, SessionFinalizer finalizer, TimeSpan? minimumSessionLength = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
        _minimumSessionLength = minimumSessionLength ?? SessionFinalizer.MinimumSessionLength;
    }

    public async ValueTask<IReadOnlyList<RecoveryOutcome>> RecoverAsync(CancellationToken ct = default)
    {
        var outcomes = new List<RecoveryOutcome>();
        IReadOnlyList<Guid> pending;
        try
        {
            pending = _store.ListPending();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            outcomes.Add(new RecoveryOutcome(Guid.Empty, RecoveryStatus.Failed, null, $"the pending files could not be listed — {ex.GetType().Name}: {ex.Message}"));
            return outcomes;
        }

        foreach (Guid guid in pending)
        {
            outcomes.Add(await RecoverOrSetAsideAsync(guid, ct).ConfigureAwait(false));
        }

        return outcomes;
    }

    private async ValueTask<RecoveryOutcome> RecoverOrSetAsideAsync(Guid guid, CancellationToken ct)
    {
        try
        {
            return await RecoverOneAsync(guid, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            string aside;
            try
            {
                _store.Quarantine(guid);
                aside = "set aside as .partial.failed";
            }
            catch (Exception move) when (move is IOException or UnauthorizedAccessException)
            {
                aside = $"and could not be set aside ({move.GetType().Name}: {move.Message}); it will be tried again at the next start";
            }

            return new RecoveryOutcome(guid, RecoveryStatus.Failed, null, $"{ex.GetType().Name}: {ex.Message} — {aside}");
        }
    }

    private async ValueTask<RecoveryOutcome> RecoverOneAsync(Guid guid, CancellationToken ct)
    {
        PartialSession? partial = _store.Read(guid);
        if (partial is null)
        {
            _store.Delete(guid);
            return new RecoveryOutcome(guid, RecoveryStatus.Unreadable, null, "no readable header");
        }

        FinalizeInput input = ToInput(partial) with { MinimumSessionLength = _minimumSessionLength, ExePath = partial.Header.ExePath };
        FinalizeOutcome result = await _finalizer.FinalizeAsync(input, ct).ConfigureAwait(false);
        _store.Delete(guid);
        return result.Status switch
        {
            FinalizeStatus.Saved => new RecoveryOutcome(guid, RecoveryStatus.Recovered, result.SessionId,
                $"{partial.Records.Count} record(s), {partial.Sensors.Count} sensor sample(s){(partial.Truncated ? ", tail truncated" : "")}"
                + (result.GameId is { } owner && owner != partial.Header.GameId ? $"; stored under games row {owner}, which holds {partial.Header.ExePath} now" : "")),
            FinalizeStatus.AlreadyStored => new RecoveryOutcome(guid, RecoveryStatus.AlreadyStored, null, "the finalize had landed"),
            FinalizeStatus.GameRemoved => new RecoveryOutcome(guid, RecoveryStatus.GameRemoved, null,
                $"games row {partial.Header.GameId} is gone and no row holds {partial.Header.ExePath}: its game was removed with its sessions"),
            _ => new RecoveryOutcome(guid, RecoveryStatus.Discarded, null,
                $"{input.Skeleton.DurationSeconds:0.#} s is under the {input.MinimumSessionLength.TotalSeconds:0} s minimum"),
        };
    }

    /// <summary>
    /// The row from the header, ended at the last flush (or the last record's clock, or — with nothing at
    /// all — at its start, which the discard rule then drops), and <c>interrupted</c> by definition.
    /// The header is written before the attach, so the tier it carries is "not yet": a file with an
    /// <c>attached</c> note, a tick or a record is a hooked session, and the note names the Overlay build.
    /// </summary>
    public static FinalizeInput ToInput(PartialSession partial)
    {
        ArgumentNullException.ThrowIfNull(partial);
        PartialHeader h = partial.Header;
        DateTimeOffset endedAt = EndedAt(partial);
        PartialNote? attached = partial.Notes.Cast<PartialNote?>().FirstOrDefault(n => n!.Value.Text.StartsWith("attached ", StringComparison.Ordinal));
        // A tick is no longer proof of a hooked session: a held Tier-2 session (2026-09-22) flushes ticks too, with no
        // records and no attached note. The attach note and the records are the proof.
        bool hooked = h.Tier == CaptureTier.Hooked || attached is not null || partial.Records.Count > 0;
        string? buildId = h.OverlayBuildId ?? BuildIdOf(attached?.Text);
        string notes = "end=Interrupted; recovered from .partial"
                       + (partial.Truncated ? " (tail truncated)" : "")
                       + (partial.Notes.Count > 0 ? "; last note: " + partial.Notes[^1].Text : "");
        var skeleton = new SessionRow
        {
            SessionGuid = h.SessionGuid,
            GameId = h.GameId,
            SnapshotId = h.SnapshotId,
            StartedAt = h.StartedAt,
            EndedAt = endedAt,
            QpcEpoch = h.QpcEpoch,
            QpcFrequency = h.QpcFrequency,
            Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
            Mode = h.Mode,
            ExitStatus = ExitStatus.Interrupted,
            CaptureNotes = notes,
            LateAttach = hooked && h.Mode == CaptureMode.Attach,
            TelemetrySource = h.TelemetryDescriptor,
            OverlayBuildId = buildId,
            LaunchWaitMs = h.LaunchWaitMs,
            DrainTicks = partial.LastTick?.DrainTicks,
            ForegroundTicks = partial.LastTick?.ForegroundTicks,
            GuardTicksPublished = partial.LastTick?.GuardTicksPublished,
        };

        AggregationInput? aggregation = !hooked ? null : new AggregationInput
        {
            Records = partial.Records,
            GapBefore = partial.GapBefore,
            Writer = partial.LastTick?.WriterState ?? default,
            QpcFrequency = h.QpcFrequency,
            TotalGaps = partial.LastTick?.TotalGaps ?? 0,
            TotalDropped = partial.LastTick?.TotalDropped ?? 0,
        };

        return new FinalizeInput { Skeleton = skeleton, Hooked = aggregation, Sensors = partial.Sensors };
    }

    /// <summary>The <c>build=…</c> word of the recorder's <c>attached</c> note, or null.</summary>
    private static string? BuildIdOf(string? attachedNote)
    {
        if (attachedNote is null)
        {
            return null;
        }

        const string key = " build=";
        int at = attachedNote.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        string id = attachedNote[(at + key.Length)..].Trim();
        return id.Length == 0 ? null : id;
    }

    private static DateTimeOffset EndedAt(PartialSession partial)
    {
        PartialHeader h = partial.Header;
        if (partial.LastTick is { } tick)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(tick.WrittenAtUnixMs);
        }

        if (partial.Records.Count > 0 && h.QpcFrequency > 0)
        {
            double seconds = (partial.Records[^1].Qpc - h.QpcEpoch) / (double)h.QpcFrequency;
            return h.StartedAt + TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        // A Tier-2 session has no ticks and no records: its sensors are the only clock it kept.
        if (partial.Sensors.Count > 0 && partial.Sensors[^1].Sample.TakenAt > h.StartedAt)
        {
            return partial.Sensors[^1].Sample.TakenAt;
        }

        return h.StartedAt;
    }
}

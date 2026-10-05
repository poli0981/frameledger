// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Watch;

/// <summary>
/// Which on-crash reporters (<see cref="CrashReporterCatalog"/>) started as a child of which process (beta.14), from the
/// watcher's own once-a-second snapshots — no process is opened and nothing is read from any. A reporter's parent is known by
/// pid, image and start: a pid Windows reused for a process that started AFTER the reporter is not its parent. The recorder
/// asks at a session's end (<see cref="FindAsync"/>); a game that just left gets two more polls, because the reporter it
/// started as it died may not have been in the last snapshot.
/// </summary>
public sealed class CrashReporterWitness : ICrashReporterSightings
{
    private static readonly TimeSpan _rememberProcess = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _rememberSighting = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _waitForPolls = TimeSpan.FromSeconds(2.5);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (string? Image, DateTimeOffset? Started, DateTimeOffset Seen)> _processes = [];
    private readonly List<CrashReporterSighting> _sightings = [];
    private readonly HashSet<(int Pid, DateTimeOffset Started)> _sighted = [];
    private long _polls;

    /// <summary>How many snapshots have been observed — what <see cref="FindAsync"/> waits on.</summary>
    public long Polls => Interlocked.Read(ref _polls);

    /// <summary>One watcher snapshot: remember who is running, and every on-crash reporter that appeared, with its parent as known.</summary>
    public void Observe(IReadOnlyList<ProcessSnapshot> snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_lock)
        {
            foreach (ProcessSnapshot p in snapshot)
            {
                _processes[p.Pid] = (p.ImagePath, p.StartedAt, now);
            }

            foreach (ProcessSnapshot p in snapshot)
            {
                if (CrashReporterCatalog.IsOnCrash(p.ImageName) && p.StartedAt is { } started && _sighted.Add((p.Pid, started)))
                {
                    _sightings.Add(Sighting(p, started));
                }
            }

            foreach (int gone in _processes.Where(e => now - e.Value.Seen > _rememberProcess).Select(static e => e.Key).ToList())
            {
                _ = _processes.Remove(gone);
            }

            _ = _sightings.RemoveAll(s => now - s.StartedAt > _rememberSighting);
            _ = _sighted.RemoveWhere(s => now - s.Started > _rememberSighting);
        }

        _ = Interlocked.Increment(ref _polls);
    }

    public async Task<CrashReporterSighting?> FindAsync(CrashQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TargetPid == 0)
        {
            return null;
        }

        if (query.TargetLeft && Polls > 0)
        {
            // The reporter a dying game starts may not be in the last snapshot yet: two more polls, at most 2.5 s.
            long target = Polls + 2;
            DateTimeOffset deadline = DateTimeOffset.UtcNow + _waitForPolls;
            while (Polls < target && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        lock (_lock)
        {
            return _sightings.FirstOrDefault(s => s.ParentPid == query.TargetPid
                                                  && s.ParentImagePath is { } parent && string.Equals(parent, query.ExePath, StringComparison.OrdinalIgnoreCase)
                                                  && s.StartedAt >= query.WindowStart && s.StartedAt <= query.WindowEnd);
        }
    }

    /// <summary>The parent as last seen — unless that pid belongs to a process that started after the reporter, which is a reused pid.</summary>
    private CrashReporterSighting Sighting(ProcessSnapshot reporter, DateTimeOffset started)
    {
        if (_processes.TryGetValue(reporter.ParentPid, out (string? Image, DateTimeOffset? Started, DateTimeOffset Seen) parent)
            && (parent.Started is null || parent.Started <= started))
        {
            return new CrashReporterSighting(reporter.ImageName, reporter.ParentPid, parent.Image, parent.Started, started);
        }

        return new CrashReporterSighting(reporter.ImageName, reporter.ParentPid, null, null, started);
    }
}

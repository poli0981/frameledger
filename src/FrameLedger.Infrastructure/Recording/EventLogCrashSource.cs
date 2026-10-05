// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using FrameLedger.Application.Recording;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// The Application log's Application Error (1000) and Windows Error Reporting (1001) events, filtered to
/// the window and the two providers, and judged by <see cref="CrashEventMatcher"/> (<c>04_CAPTURE</c> §Crash &amp; exit
/// classification) — since beta.14, which counted any such record whose text contained the executable's name.
/// </summary>
/// <remarks>
/// Read-only, unprivileged (the Application log is readable by any interactive user), bounded by the
/// window in the query itself so a busy log is not walked. A log that cannot be read answers false —
/// absence of evidence — and the exit code still decides on its own.
/// </remarks>
public sealed class EventLogCrashSource : ICrashEventSource
{
    private const string _log = "Application";

    public bool FoundCrash(CrashQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        string from = query.WindowStart.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        string to = query.WindowEnd.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        string xpath = "*[System[Provider[@Name='" + CrashEventMatcher.ApplicationErrorProvider + "' or @Name='" + CrashEventMatcher.WerProvider + "']"
                       + $" and (EventID=1000 or EventID=1001) and TimeCreated[@SystemTime>='{from}' and @SystemTime<='{to}']]]";
        try
        {
            using var reader = new EventLogReader(new EventLogQuery(_log, PathType.LogName, xpath));
            for (EventRecord? record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
            {
                using (record)
                {
                    var read = new CrashLogRecord(record.Id, record.ProviderName, [.. record.Properties.Select(static p => p.Value)]);
                    if (CrashEventMatcher.Matches(read, query))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}

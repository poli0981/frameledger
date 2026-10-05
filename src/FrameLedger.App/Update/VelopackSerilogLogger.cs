// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Serilog;
using Serilog.Events;
using Velopack.Logging;

namespace FrameLedger.App.Update;

/// <summary>
/// Velopack's own diagnostics in the App's log (beta.14), each line prefixed <c>velopack:</c> — whether an update came as a
/// delta or as the full package, which processes it stopped, why an apply failed. Until then they were only in Velopack's
/// file under <c>%LOCALAPPDATA%\velopack</c>, which the bug report does not collect. Set in <c>Program.Main</c>, before Serilog
/// is configured: each line reads <see cref="Log.Logger"/> when it is written, so the early ones go nowhere and the rest arrive.
/// </summary>
internal sealed class VelopackSerilogLogger : IVelopackLogger
{
    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception) =>
        Serilog.Log.Logger.Write(Level(logLevel), exception, "velopack: {Message}", message ?? string.Empty);

    internal static LogEventLevel Level(VelopackLogLevel level) => level switch
    {
        VelopackLogLevel.Trace => LogEventLevel.Verbose,
        VelopackLogLevel.Debug => LogEventLevel.Debug,
        VelopackLogLevel.Information => LogEventLevel.Information,
        VelopackLogLevel.Warning => LogEventLevel.Warning,
        VelopackLogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal,
    };
}

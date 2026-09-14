using Serilog.Core;
using Serilog.Events;

namespace FrameLedger.App.Services;

/// <summary>The App's log level at runtime: <c>log.debug</c> flips it without a restart (the Agent reads the key at its next start).</summary>
public static class LoggingLevel
{
    public static LoggingLevelSwitch Switch { get; } = new(LogEventLevel.Information);

    public static void SetDebug(bool debug) => Switch.MinimumLevel = debug ? LogEventLevel.Debug : LogEventLevel.Information;
}

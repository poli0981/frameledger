namespace FrameLedger.App.Services;

/// <summary>The Logs page's level filter, over the <c>[HH:mm:ss.fff LVL]</c> prefix of <c>10_LOGGING</c>'s template.</summary>
public enum LogLevelFilter
{
    All,
    WarningAndAbove,
    ErrorAndAbove,
}

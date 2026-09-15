namespace FrameLedger.App.Services;

/// <summary>The answer to the bug report's optional crash dump (P4 PR-9, <c>10_LOGGING</c> §Bug report flow step 2).</summary>
public enum CrashDumpChoice
{
    /// <summary>The dialog was closed: no bundle is written.</summary>
    Cancel,

    /// <summary>Continue without the dump: the checkbox was left clear, which is how it starts.</summary>
    LeaveOut,

    /// <summary>Continue with the dump: the user ticked it.</summary>
    Include,
}

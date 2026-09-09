namespace FrameLedger.Application.Consent;

/// <summary>
/// Which operator-facing surface is showing <see cref="OperatorDisclosure"/>: the text's first line and the
/// provenance it stamps differ, and nothing else does.
/// </summary>
public enum OperatorSurface
{
    /// <summary>The unshipped capture host's <c>consent grant</c> (P0, 2026-08-06).</summary>
    UnshippedHost = 0,

    /// <summary>The Agent's <c>--console consent grant</c> (P2 PR-F, HANDOFF §P2 decision D4).</summary>
    AgentConsole = 1,
}

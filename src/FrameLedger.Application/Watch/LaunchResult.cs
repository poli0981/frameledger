using System.Runtime.InteropServices;

namespace FrameLedger.Application.Watch;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LaunchResult(LaunchOutcome Outcome, Guid? SessionGuid)
{
    public static LaunchResult Of(LaunchOutcome outcome) => new(outcome, null);
}

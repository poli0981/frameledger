using System.Runtime.InteropServices;

namespace FrameLedger.App.Services;

[StructLayout(LayoutKind.Auto)]
public readonly record struct GuardBypassResult(GuardBypassOutcome Outcome, string? Detail = null);

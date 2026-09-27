using FluentAssertions;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Io;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Startup;

/// <summary>
/// Nothing the Agent does ENABLES a privilege (beta.10, the admin mode, D34). An elevated token holds the debug and
/// load-driver privileges disabled; <c>src/</c> may not enable one (<c>tools/chokepoint-check.ps1</c>), and this checks what
/// the code actually does, third-party code included — the resolver opening processes, LibreHardwareMonitor opening the CPU
/// group as the elevated Agent's CPU temperature does.
/// </summary>
/// <remarks>
/// Meaningful on an elevated run, which CI is: an unelevated token does not hold these privileges at all, so there the
/// assertions pass by construction. If a library ever enables one, this goes red on CI — and that is a question for the
/// owner, not a line to relax.
/// </remarks>
public sealed class PrivilegeFootprintTests
{
    private static readonly string[] _powerful =
        ["SeDebugPrivilege", "SeLoadDriverPrivilege", "SeTcbPrivilege", "SeTakeOwnershipPrivilege", "SeBackupPrivilege", "SeRestorePrivilege"];

    [Fact]
    public void OpeningTargetsAndTheCpuSensorsEnablesNoPowerfulPrivilege()
    {
        IReadOnlyList<string> before = TokenPrivileges.Enabled();
        before.Should().Contain("SeChangeNotifyPrivilege", "every token has it enabled; an empty answer would be a reader that read nothing");
        before.Should().NotContain(_powerful, "nothing has run yet");

        _ = new TargetResolver().Resolve(ExecutableIdentity.Normalise(Environment.ProcessPath!), out _);
        using (var cpu = new LhmCpuTemperatureReader(new LhmComputerAdapter(enableCpuAndMemory: true, enableGpu: false)))
        {
            _ = cpu.Read();
        }

        TokenPrivileges.Enabled().Should().NotContain(_powerful,
            $"opening a target and the CPU sensors enabled a privilege (elevated run: {Environment.IsPrivilegedProcess})");
    }
}

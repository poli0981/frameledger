using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Tests.Capture;

internal sealed class DelegateKillSwitch(Func<bool> engaged) : IKillSwitch
{
    public ValueTask<bool> IsEngagedAsync(CancellationToken ct = default) => ValueTask.FromResult(engaged());
}

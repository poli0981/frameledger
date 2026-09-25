using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Tests.AntiCheat;

/// <summary>D33: <c>hooking.usermode_ac_exceptions</c> as a test says it is; every read is counted.</summary>
internal sealed class FakeExceptionSwitch(bool on = false) : IUserModeExceptionSwitch
{
    public bool On { get; set; } = on;

    public int Reads { get; private set; }

    public ValueTask<bool> IsOnAsync(CancellationToken ct = default)
    {
        Reads++;
        return ValueTask.FromResult(On);
    }
}

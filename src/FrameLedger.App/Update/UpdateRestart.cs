namespace FrameLedger.App.Update;

/// <summary>Set by Velopack's restarted hook in <c>Program.Main</c>, read once by <see cref="UpdateHostedService"/>: this process is the first run after an update.</summary>
internal static class UpdateRestart
{
    public static bool Detected { get; set; }
}

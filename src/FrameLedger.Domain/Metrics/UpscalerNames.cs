namespace FrameLedger.Domain.Metrics;

/// <summary>
/// The vendor's own quality value as the name the vendor's menu uses (<c>03_METRICS</c> §Upscaling promised this type
/// from the start; until 2026-09-21 the UI printed the raw integer beside the upscaler).
/// </summary>
/// <remarks>
/// <para>
/// <b>One producer exists, so one table exists.</b> The only writer of <c>upscalerQuality</c> is the Streamline route:
/// <c>sl::DLSSMode</c> as the title chained it into <c>slEvaluateFeature</c> (<c>fl_sl_inputs.h</c>
/// <c>QualityFromMode</c>), where <c>eOff</c> and anything at or past <c>eCount</c> are already folded to "not told" and
/// never reach a row. The ffx-api dispatch carries no quality mode, and XeSS has no hook, so their bytes are always
/// "not told" and no table for them would have a row to name.
/// </para>
/// <para>
/// A value this table does not know is returned as it is stored: a number nobody can name is still what was measured,
/// and dropping it would turn a newer SDK's preset into "nothing was told".
/// </para>
/// <para>
/// This is the MEASURED byte. A label derived from the render/output ratio is a different thing and an owner decision
/// not taken (<c>HANDOFF</c> §7a); nothing here infers.
/// </para>
/// </remarks>
public static class UpscalerNames
{
    /// <summary>
    /// The preset's name, the stored value when it has none, or null when the row stores no quality at all.
    /// </summary>
    /// <param name="upscaler">The row's upscaler token (<c>dlss</c>, <c>fsr3</c>, …).</param>
    /// <param name="quality">The row's quality value: the vendor's integer, invariant.</param>
    public static string? Quality(string? upscaler, string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return null;
        }

        if (!string.Equals(upscaler, "dlss", StringComparison.Ordinal))
        {
            return quality;
        }

        return quality switch
        {
            "1" => "Performance",
            "2" => "Balanced",
            "3" => "Quality",
            "4" => "Ultra Performance",
            "5" => "Ultra Quality",
            "6" => "DLAA",
            _ => quality,
        };
    }
}

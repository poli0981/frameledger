namespace FrameLedger.Application.Settings;

/// <summary>How a <see cref="SettingDefinition"/>'s text value is read and validated.</summary>
public enum SettingKind
{
    /// <summary><c>"1"</c> is true; anything else, or no row, is false — the kill switch's rule (<c>06_DATA_MODEL</c>).</summary>
    Boolean,

    /// <summary>An invariant-culture integer within <see cref="SettingDefinition.Minimum"/>..<see cref="SettingDefinition.Maximum"/>.</summary>
    WholeNumber,

    /// <summary>One of <see cref="SettingDefinition.Choices"/>, compared ordinally.</summary>
    Choice,
}

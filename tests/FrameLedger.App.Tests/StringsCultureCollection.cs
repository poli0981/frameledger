namespace FrameLedger.App.Tests;

/// <summary>
/// The collection every test class that WRITES the process-wide <c>Strings.Culture</c> belongs to, so they run one after
/// another. The property is a static the App sets once at start-up; eight test classes set it to <c>en</c> around their
/// assertions and <see cref="StringsTests"/> sets it to <c>vi</c>, and as separate collections they ran in parallel: the
/// first gate run on xUnit.net v3 4.0.1 (2026-09-21) read "Settings" where it had just written <c>vi</c>. The race was
/// always there; the new scheduler found it. An implicit collection: no fixture, so no definition class is needed.
/// Since beta.8 it also holds the classes that write <c>FpsDecimals.Two</c> or read FPS text the setting changes — the
/// same kind of process-wide formatting state.
/// </summary>
internal static class StringsCultureCollection
{
    public const string Name = "strings-culture";
}

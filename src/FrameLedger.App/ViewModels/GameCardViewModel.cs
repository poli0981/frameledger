using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One library card (<c>08_UI</c> §Games): name, platform and engine badges, playtime, last played, hooking state — or, since
/// 2026-09-23, "Not recorded" in its place for an entry whose recording is off, and since 2026-09-25 "Anti-cheat" for one
/// the guard found anti-cheat in. The sparkline is PR-6's.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class GameCardViewModel
{
    public GameCardViewModel(GameCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Id = card.Row.Id;
        Name = card.Row.Name;
        PlatformText = Formats.Platform(card.Row.Platform);
        EngineText = Formats.Engine(card.Row.Engine);
        Recorded = card.Row.RecordSessions;
        HookOn = Recorded && card.Row.HookEnabled;
        // An entry the guard found anti-cheat in says so (beta.8): its hooking is off for good, and "Hooking off" read as
        // a switch the user could still flip. Not recorded still comes first — nothing runs for it at all.
        AntiCheat = Recorded && card.Row.BlockedByGuard;
        // D33: a blocked game with a user-mode exception on its row says so; whether Settings' option lets it apply is the
        // game page's to say.
        Excepted = AntiCheat && card.Row.AcException.IsGranted;
        HookText = !Recorded ? Strings.Games_Card_NotRecorded
            : Excepted ? Strings.Games_Card_Exception
            : AntiCheat ? Strings.Games_Card_AntiCheat
            : HookOn ? Strings.Games_Card_HookOn : Strings.Games_Card_HookOff;
        SessionCount = card.Summary?.SessionCount ?? 0;
        TotalSeconds = card.Summary?.TotalSeconds ?? 0;
        LastPlayedAt = card.Summary?.LastPlayedAt;
        SessionsText = string.Format(CultureInfo.CurrentCulture, Strings.Games_Card_Sessions_Format, SessionCount);
        PlaytimeText = string.Format(CultureInfo.CurrentCulture, Strings.Games_Card_Playtime_Format, Formats.Playtime(TotalSeconds));
        LastPlayedText = LastPlayedAt is DateTimeOffset at
            ? string.Format(CultureInfo.CurrentCulture, Strings.Games_Card_LastPlayed_Format, Formats.Date(at))
            : Strings.Games_Card_NeverPlayed;
    }

    public GameCard Card { get; }

    public long Id { get; }

    public string Name { get; }

    public string PlatformText { get; }

    public bool HasPlatform => PlatformText.Length > 0;

    public string EngineText { get; }

    public bool HasEngine => EngineText.Length > 0;

    /// <summary>Whether FrameLedger watches for this entry's program (schema 0009).</summary>
    public bool Recorded { get; }

    public bool HookOn { get; }

    /// <summary>
    /// The pill says "Anti-cheat" (caution fill, <c>Styles/FrameLedger.xaml</c>): a guard finding about this game is on its row
    /// (<c>19_SAFETY</c> §What a finding does to the game) and the entry is recorded — "Not recorded" says more.
    /// </summary>
    public bool AntiCheat { get; }

    /// <summary>D33: the anti-cheat game carries a user-mode exception (<c>games.ac_exception_at</c>); the pill says "Exception".</summary>
    public bool Excepted { get; }

    public string HookText { get; }

    public long SessionCount { get; }

    public double TotalSeconds { get; }

    public DateTimeOffset? LastPlayedAt { get; }

    public string SessionsText { get; }

    public string PlaytimeText { get; }

    public string LastPlayedText { get; }
}

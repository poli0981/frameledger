using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>One library card (<c>08_UI</c> §Games): name, platform and engine badges, playtime, last played, hooking state. The sparkline is PR-6's.</summary>
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
        HookOn = card.Row.HookEnabled;
        HookText = HookOn ? Strings.Games_Card_HookOn : Strings.Games_Card_HookOff;
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

    public bool HookOn { get; }

    public string HookText { get; }

    public long SessionCount { get; }

    public double TotalSeconds { get; }

    public DateTimeOffset? LastPlayedAt { get; }

    public string SessionsText { get; }

    public string PlaytimeText { get; }

    public string LastPlayedText { get; }
}

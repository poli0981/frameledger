// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One library entry (<c>08_UI</c> §Games), as a card in the grid or a row in the list: name, platform and engine badges,
/// playtime, last played, hooking state — or, since 2026-09-23, "Not recorded" in its place for an entry whose recording is
/// off, and since 2026-09-25 "Anti-cheat" for one the guard found anti-cheat in; since beta.11 "Not installed" beside it
/// when its executable is not on disk (<see cref="GameLibrary.ExecutableExists"/>, read when the library is listed, never
/// stored), with the game page's own words for why (<see cref="Formats.ExecutableMissingText"/>). Since beta.34 a card is
/// shown before its executable is checked and gets the pill when the check answers (<see cref="SetInstalled"/>): a drive
/// that sleeps took a second to say, and no card appeared until every one had.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class GameCardViewModel : ObservableObject
{
    /// <summary>beta.11: the "Not installed" pill shows — the file, or the drive it was on, is not there now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    private bool _notInstalled;

    /// <summary>What the pill means here — the drive that is not connected, or the path the file is gone from; null when installed.</summary>
    [ObservableProperty]
    private string? _notInstalledToolTip;

    public GameCardViewModel(GameCard card, bool installed = true)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        _notInstalled = !installed;
        _notInstalledToolTip = installed ? null : Formats.ExecutableMissingText(card.Row.Fingerprint.ExePath);
        Id = card.Row.Id;
        Name = card.Row.Name;
        PlatformToken = card.Row.Platform;
        EngineToken = card.Row.Engine;
        PlatformText = Formats.Platform(card.Row.Platform);
        EngineText = Formats.Engine(card.Row.Engine, card.Row.EngineVersionSource);
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
        PlaytimeValue = Formats.Playtime(TotalSeconds);
        PlaytimeText = string.Format(CultureInfo.CurrentCulture, Strings.Games_Card_Playtime_Format, PlaytimeValue);
        LastPlayedValue = LastPlayedAt is DateTimeOffset when ? Formats.Date(when) : Strings.Games_Card_NeverPlayed;
        LastPlayedText = LastPlayedAt is DateTimeOffset at
            ? string.Format(CultureInfo.CurrentCulture, Strings.Games_Card_LastPlayed_Format, Formats.Date(at))
            : Strings.Games_Card_NeverPlayed;
    }

    public GameCard Card { get; }

    public long Id { get; }

    public string Name { get; }

    public string PlatformText { get; }

    /// <summary>The store's token (<c>steam</c>, <c>gog</c>, <c>epic</c>, <c>itch</c>, <c>xbox</c>; <c>none</c> for a game added by hand) — the store filter's key.</summary>
    public string PlatformToken { get; }

    /// <summary>The detected engine's id, or null — the engine filter's key.</summary>
    public string? EngineToken { get; }

    /// <summary>The card's one state, as its pill says it (beta.34, D85).</summary>
    public GamesStateFilter State => !Recorded ? GamesStateFilter.NotRecorded
        : Excepted ? GamesStateFilter.Exception
        : AntiCheat ? GamesStateFilter.AntiCheat
        : HookOn ? GamesStateFilter.HookingOn : GamesStateFilter.HookingOff;

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

    /// <summary>The list view's playtime cell: the duration alone.</summary>
    public string PlaytimeValue { get; }

    /// <summary>The list view's last-played cell: the date alone, or "not played yet".</summary>
    public string LastPlayedValue { get; }

    /// <summary>The list view's status cell: what the hook pill says, and "Not installed" after it when that is so.</summary>
    public string StatusText => NotInstalled ? HookText + " · " + Strings.Games_Card_NotInstalled : HookText;

    /// <summary>The executable check's answer, when it comes (beta.34) — with the reason, read off the UI thread with it.</summary>
    public void SetInstalled(bool installed, string? whyNot)
    {
        NotInstalled = !installed;
        NotInstalledToolTip = installed ? null : whyNot;
    }

    /// <summary>
    /// Whether <paramref name="other"/> — the same game read again — would look the same on its card and in its row, and
    /// play the same executable (beta.34): a reload keeps the card it already has, and rebuilds only one that changed.
    /// </summary>
    public bool LooksLike(GameCardViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Id == other.Id
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(PlatformText, other.PlatformText, StringComparison.Ordinal)
            && string.Equals(EngineText, other.EngineText, StringComparison.Ordinal)
            && string.Equals(HookText, other.HookText, StringComparison.Ordinal)
            && string.Equals(SessionsText, other.SessionsText, StringComparison.Ordinal)
            && string.Equals(PlaytimeText, other.PlaytimeText, StringComparison.Ordinal)
            && string.Equals(LastPlayedText, other.LastPlayedText, StringComparison.Ordinal)
            && string.Equals(Card.Row.Fingerprint.ExePath, other.Card.Row.Fingerprint.ExePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Card.Row.Platform, other.Card.Row.Platform, StringComparison.Ordinal)
            && string.Equals(Card.Row.StoreId, other.Card.Row.StoreId, StringComparison.Ordinal);
    }

    /// <summary>The card and the list's row as a screen reader reads them (beta.35, D88): the game and its state.</summary>
    public string AccessibleName => SpokenText.List(Name, StatusText);

    /// <summary>The card's Play button, naming the game it starts.</summary>
    public string PlayName => SpokenText.Pair(Strings.Play_Button, Name);

    public override string ToString() => AccessibleName;
}

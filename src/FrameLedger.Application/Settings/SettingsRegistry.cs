namespace FrameLedger.Application.Settings;

/// <summary>
/// The settings registry — HANDOFF §P3 decision D16, closing <c>20_OPEN_QUESTIONS</c> §G's row and written into
/// <c>06_DATA_MODEL</c> §settings with P3 PR-3. Every key the <c>settings</c> table may hold, with its kind,
/// default and range. The UI owns <c>ui.*</c>, <c>update.*</c>, <c>privacy.*</c> and <c>log.*</c>; the Agent
/// reads <c>hooking.*</c>, <c>capture.*</c>, <c>telemetry.*</c> and <c>retention.*</c> at each session start.
/// </summary>
public static class SettingsRegistry
{
    /// <summary><c>09_I18N</c>: en (neutral), vi, ja.</summary>
    public static readonly SettingDefinition UiLanguage = new()
    {
        Key = "ui.language",
        Kind = SettingKind.Choice,
        Default = "en",
        Choices = ["en", "vi", "ja"],
    };

    /// <summary><c>16_WPFUI_SYNTAX</c> §Theme rules.</summary>
    public static readonly SettingDefinition UiTheme = new()
    {
        Key = "ui.theme",
        Kind = SettingKind.Choice,
        Default = "system",
        Choices = ["system", "light", "dark"],
    };

    public static readonly SettingDefinition UiStartWithWindows = new()
    {
        Key = "ui.start_with_windows",
        Kind = SettingKind.Boolean,
        Default = "0",
    };

    public static readonly SettingDefinition UiMinimizeToTray = new()
    {
        Key = "ui.minimize_to_tray",
        Kind = SettingKind.Boolean,
        Default = "0",
    };

    /// <summary>
    /// Whether a game the guard found anti-cheat in shows its Hooking card, or only the finding (beta.8, owner request
    /// 2026-09-25). On by default: the card's switch can never be turned on for such a game, so hiding it leaves the
    /// finding — which is always shown (FR-2.2) — and nothing that looks usable.
    /// </summary>
    public static readonly SettingDefinition UiHideAntiCheatHooking = new()
    {
        Key = "ui.hide_anticheat_hooking",
        Kind = SettingKind.Boolean,
        Default = "1",
    };

    /// <summary>
    /// Whether every FPS figure the App shows has two decimals ("62.40") or is a whole number ("62") — beta.8, owner request
    /// 2026-09-25. Off by default: the whole number is what every page showed until then.
    /// </summary>
    public static readonly SettingDefinition UiFpsDecimals = new()
    {
        Key = "ui.fps_decimals",
        Kind = SettingKind.Boolean,
        Default = "0",
    };

    /// <summary>Whether the Agent's watcher records tracked games that the App did not launch (FR-3.1/FR-3.3).</summary>
    public static readonly SettingDefinition CaptureBackground = new()
    {
        Key = "capture.background",
        Kind = SettingKind.Boolean,
        Default = "1",
        AgentReads = true,
    };

    /// <summary>FR-2.4. The one key that existed before the registry (P2 PR-F, <c>SettingsKillSwitch</c>): exactly "1" is engaged.</summary>
    public static readonly SettingDefinition HookingKillSwitch = new()
    {
        Key = "hooking.kill_switch",
        Kind = SettingKind.Boolean,
        Default = "0",
        AgentReads = true,
    };

    /// <summary>
    /// D33 (owner decision 2026-09-26): whether a game's user-mode anti-cheat exception may apply at all. Off by default;
    /// on, the user still grants each eligible game through its disclosure, and off suspends every grant without deleting
    /// any (<c>19_SAFETY</c> §The user-mode exception). Exactly "1" is on.
    /// </summary>
    public static readonly SettingDefinition HookingUserModeExceptions = new()
    {
        Key = "hooking.usermode_ac_exceptions",
        Kind = SettingKind.Boolean,
        Default = "0",
        AgentReads = true,
    };

    /// <summary>FR-3.6: sessions shorter than this are discarded. Seconds.</summary>
    public static readonly SettingDefinition CaptureMinSessionSeconds = new()
    {
        Key = "capture.min_session_s",
        Kind = SettingKind.WholeNumber,
        Default = "30",
        Minimum = 5,
        Maximum = 600,
        AgentReads = true,
    };

    /// <summary>FR-3.5: telemetry at 1 Hz, configurable 0.5–2 s. Milliseconds.</summary>
    public static readonly SettingDefinition TelemetryIntervalMs = new()
    {
        Key = "telemetry.interval_ms",
        Kind = SettingKind.WholeNumber,
        Default = "1000",
        Minimum = 500,
        Maximum = 2000,
        AgentReads = true,
    };

    /// <summary><c>06_DATA_MODEL</c> §Retention: raw blobs for the last N sessions per game; 0 = unlimited.</summary>
    public static readonly SettingDefinition RetentionRawSessionsPerGame = new()
    {
        Key = "retention.raw_sessions_per_game",
        Kind = SettingKind.WholeNumber,
        Default = "20",
        Minimum = 0,
        Maximum = 10_000,
        AgentReads = true,
    };

    /// <summary><c>11_UPDATER</c>: the release feed's channel — <c>stable</c> is the releases GitHub does not mark pre-release, <c>beta</c> adds the ones it does (P4 PR-5 reads it).</summary>
    public static readonly SettingDefinition UpdateChannel = new()
    {
        Key = "update.channel",
        Kind = SettingKind.Choice,
        Default = "stable",
        Choices = ["stable", "beta"],
    };

    /// <summary><c>11_UPDATER</c> §Flow: the startup silent check, "if enabled" — on by default, one request to GitHub Releases after start (P4 PR-5).</summary>
    public static readonly SettingDefinition UpdateAutoCheck = new()
    {
        Key = "update.auto_check",
        Kind = SettingKind.Boolean,
        Default = "1",
    };

    /// <summary>CLAUDE.md rule 8: the opt-in store-metadata fetch, off by default.</summary>
    public static readonly SettingDefinition PrivacyOnlineMetadata = new()
    {
        Key = "privacy.online_metadata",
        Kind = SettingKind.Boolean,
        Default = "0",
    };

    /// <summary><c>10_LOGGING</c>: Debug level on the file sinks.</summary>
    public static readonly SettingDefinition LogDebug = new()
    {
        Key = "log.debug",
        Kind = SettingKind.Boolean,
        Default = "0",
    };

    /// <summary>Every definition, in the order <c>06_DATA_MODEL</c> lists them.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        UiLanguage, UiTheme, UiStartWithWindows, UiMinimizeToTray, UiHideAntiCheatHooking, UiFpsDecimals,
        CaptureBackground, HookingKillSwitch, HookingUserModeExceptions, CaptureMinSessionSeconds, TelemetryIntervalMs, RetentionRawSessionsPerGame,
        UpdateChannel, UpdateAutoCheck, PrivacyOnlineMetadata, LogDebug,
    ];

    private static readonly Dictionary<string, SettingDefinition> _byKey = All.ToDictionary(static d => d.Key, StringComparer.Ordinal);

    /// <summary>The definition for a key, or null for a key the registry does not know — which no writer may use.</summary>
    public static SettingDefinition? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _byKey.GetValueOrDefault(key);
    }
}

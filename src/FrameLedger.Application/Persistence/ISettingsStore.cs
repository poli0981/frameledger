namespace FrameLedger.Application.Persistence;

/// <summary>
/// The <c>settings</c> key/value table, raw. ~~The key list itself is <c>20_OPEN_QUESTIONS</c> §G's "Settings
/// registry", still open.~~ The key list is <c>Application.Settings.SettingsRegistry</c> since P3 PR-3
/// (2026-09-13, HANDOFF §P3 decision D16); go through <c>RegisteredSettings</c>, which validates writes and
/// tolerates reads, unless you are the kill switch, which predates it and keeps its own key constant.
/// </summary>
public interface ISettingsStore
{
    ValueTask<string?> GetAsync(string key, CancellationToken ct = default);

    ValueTask SetAsync(string key, string value, CancellationToken ct = default);
}

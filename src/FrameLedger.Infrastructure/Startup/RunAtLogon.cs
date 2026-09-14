using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// FR-10 "start with Windows" as the per-user <c>Run</c> value (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>)
/// — this user only, never HKLM, never a service, never admin. The value is the executable's quoted path, so a
/// moved install reads as "not set" and the next <see cref="Set"/> replaces the stale entry rather than leaving
/// Windows launching something else. The value name is a parameter so a test can use its own and clean it up.
/// </summary>
public sealed class RunAtLogon
{
    public const string DefaultValueName = "FrameLedger";

    private const string _key = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    public RunAtLogon(string valueName = DefaultValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        _valueName = valueName;
    }

    public bool IsSet(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_key, writable: false);
        return key?.GetValue(_valueName) is string value && string.Equals(value, Quote(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void Set(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(_key, writable: true);
        key.SetValue(_valueName, Quote(executablePath), RegistryValueKind.String);
    }

    public void Clear()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_key, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    private static string Quote(string path) => "\"" + path + "\"";
}

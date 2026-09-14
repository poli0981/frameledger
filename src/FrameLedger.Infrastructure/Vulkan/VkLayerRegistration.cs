using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Vulkan;

/// <summary>
/// The layer's per-user registration (<c>17_HOOK_ENGINE</c> §Vulkan: <c>HKCU\SOFTWARE\Khronos\Vulkan\ImplicitLayers</c>,
/// never HKLM, never admin): one DWORD value named by the manifest's full path, 0 = enabled. The loader reads
/// this key in every Vulkan process of this user, which is why <c>12_BUILD</c> §The Vulkan layer is not
/// registered at install time makes registering a deliberate act — and why the layer, once loaded, stays inert
/// without <c>FRAMELEDGER_ENABLE_VK_LAYER=1</c>, which only a launch sets. The key path is a parameter so a test
/// can exercise the mechanics under its own key rather than the loader's.
/// </summary>
public sealed class VkLayerRegistration
{
    public const string DefaultKeyPath = @"SOFTWARE\Khronos\Vulkan\ImplicitLayers";

    private readonly string _keyPath;

    public VkLayerRegistration(string keyPath = DefaultKeyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _keyPath = keyPath;
    }

    /// <summary>True when a value named <paramref name="manifestPath"/> exists and is 0 (enabled).</summary>
    public bool IsRegistered(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        return key?.GetValue(manifestPath) is int value && value == 0;
    }

    /// <summary>Every manifest path registered under the key that names our layer's manifest file, whatever directory it sits in (a moved install leaves stale ones).</summary>
    public IReadOnlyList<string> RegisteredManifests()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        if (key is null)
        {
            return [];
        }

        return [.. key.GetValueNames().Where(static n => n.EndsWith(VkLayerLaunchEnvironment.ManifestFileName, StringComparison.OrdinalIgnoreCase))];
    }

    public void Register(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        key.SetValue(manifestPath, 0, RegistryValueKind.DWord);
    }

    /// <summary>Removes the value for <paramref name="manifestPath"/> and any stale value naming our manifest file elsewhere; true when something was removed.</summary>
    public bool Unregister(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        if (key is null)
        {
            return false;
        }

        bool removed = false;
        foreach (string name in key.GetValueNames())
        {
            if (string.Equals(name, manifestPath, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(VkLayerLaunchEnvironment.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue(name, throwOnMissingValue: false);
                removed = true;
            }
        }

        return removed;
    }
}

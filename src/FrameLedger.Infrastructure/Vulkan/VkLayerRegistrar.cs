using FrameLedger.Application.Vulkan;

namespace FrameLedger.Infrastructure.Vulkan;

/// <summary>
/// <see cref="IVkLayerRegistrar"/> over <see cref="VkLayerRegistration"/> and the manifest writer, for the product
/// directory: the layer DLL beside the Agent, the manifest under <c>%LOCALAPPDATA%\FrameLedger\vklayer</c>
/// (P4 PR-2). The same two calls <c>--register-vklayer</c> / <c>--unregister-vklayer</c> make, so the automatic
/// path and the repair tool cannot disagree about what "registered" means.
/// </summary>
public sealed class VkLayerRegistrar(string layerDirectory, string layerDllPath, string keyPath = VkLayerRegistration.DefaultKeyPath) : IVkLayerRegistrar
{
    private readonly string _layerDirectory = layerDirectory ?? throw new ArgumentNullException(nameof(layerDirectory));
    private readonly string _layerDllPath = layerDllPath ?? throw new ArgumentNullException(nameof(layerDllPath));
    private readonly VkLayerRegistration _registration = new(keyPath);

    public string ManifestPath => Path.Combine(_layerDirectory, VkLayerLaunchEnvironment.ManifestFileName);

    public bool IsStaged => File.Exists(_layerDllPath);

    public bool IsRegistered() => _registration.IsRegistered(ManifestPath);

    public void Register() => _registration.Register(VkLayerLaunchEnvironment.WriteManifest(_layerDirectory, _layerDllPath));

    public bool Unregister() => _registration.Unregister(ManifestPath);
}

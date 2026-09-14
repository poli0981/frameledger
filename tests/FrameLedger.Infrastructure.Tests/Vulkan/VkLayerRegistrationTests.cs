using FluentAssertions;
using FrameLedger.Infrastructure.Vulkan;
using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Tests.Vulkan;

/// <summary>The HKCU registration's mechanics under a test's own key — never the loader's, which every Vulkan process of this user reads.</summary>
public sealed class VkLayerRegistrationTests
{
    [Fact]
    public void RegistersReadsBackAndUnregistersIncludingAStaleEntry()
    {
        string key = @"SOFTWARE\FrameLedger.Test\" + Guid.NewGuid().ToString("N") + @"\ImplicitLayers";
        var registration = new VkLayerRegistration(key);
        string manifest = @"C:\Users\x\AppData\Local\FrameLedger\vklayer\" + VkLayerLaunchEnvironment.ManifestFileName;
        string stale = @"D:\old\vklayer\" + VkLayerLaunchEnvironment.ManifestFileName;
        try
        {
            registration.IsRegistered(manifest).Should().BeFalse();
            registration.RegisteredManifests().Should().BeEmpty();

            registration.Register(manifest);
            registration.Register(stale);

            registration.IsRegistered(manifest).Should().BeTrue();
            registration.RegisteredManifests().Should().BeEquivalentTo([manifest, stale]);

            registration.Unregister(manifest).Should().BeTrue();

            registration.IsRegistered(manifest).Should().BeFalse();
            registration.RegisteredManifests().Should().BeEmpty("a stale entry naming our manifest elsewhere goes with it");
            registration.Unregister(manifest).Should().BeFalse();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\FrameLedger.Test", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void TheDefaultKeyIsTheLoadersPerUserOne()
    {
        VkLayerRegistration.DefaultKeyPath.Should().Be(@"SOFTWARE\Khronos\Vulkan\ImplicitLayers", "17_HOOK_ENGINE §Vulkan: HKCU, never HKLM");
        FluentActions.Invoking(static () => new VkLayerRegistration(" ")).Should().Throw<ArgumentException>();
    }
}

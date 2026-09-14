using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Vulkan;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.Vulkan;

/// <summary>
/// <c>12_BUILD</c> §The Vulkan layer is not registered at install time, enforced (P4 PR-2): registered only while a
/// hook-enabled game references the Vulkan loader, unregistered when the last such game is disabled, nothing when
/// the layer is not staged, and idempotent.
/// </summary>
public sealed class VkLayerReconcilerTests
{
    private const string _vulkanGame = @"C:\Games\V\v.exe";
    private const string _d3dGame = @"C:\Games\D\d.exe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeConsent : IGameConsentStore
    {
        public HashSet<string> Enabled { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ValueTask<GameConsentRecord> FindAsync(string normalisedExePath, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<GameConsentRecord>> ListEnabledAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<GameConsentRecord>>([.. Enabled.Select(static p => GameConsentRecord.Stored(
                new ExecutableFingerprint { ExePath = p, SizeBytes = 1, MtimeUnixMs = 1 }, hookEnabled: true, DateTimeOffset.UnixEpoch,
                ConsentProvenance.ConsentDialog, "consent-dialog/1", null, false, DateTimeOffset.UnixEpoch))]);

        public ValueTask<ConsentWriteOutcome> RecordOperatorAcknowledgementAsync(OperatorAcknowledgement acknowledgement, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ConsentWriteOutcome> RevokeAsync(string normalisedExePath, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ConsentWriteOutcome> RecordGuardBlockAsync(ExecutableFingerprint fingerprint, AntiCheatVerdict verdict, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeRegistrar : IVkLayerRegistrar
    {
        public bool IsStaged { get; set; } = true;

        public bool Registered { get; set; }

        public List<string> Calls { get; } = [];

        public bool IsRegistered() => Registered;

        public void Register()
        {
            Calls.Add("register");
            Registered = true;
        }

        public bool Unregister()
        {
            Calls.Add("unregister");
            bool was = Registered;
            Registered = false;
            return was;
        }
    }

    private static async Task<(VkLayerReconciler Reconciler, FakeConsent Consent, FakeRegistrar Registrar, List<string> Log)> BuildAsync()
    {
        var games = new FakeGameRepository();
        GameRow v = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _vulkanGame, SizeBytes = 1, MtimeUnixMs = 1 }, "V", Ct).ConfigureAwait(false);
        await games.ApplyDetectionAsync(v.Id, new DetectionWrite { CapabilityIds = ["dlss", "vulkan"], RulesVersion = "r", ExeSizeBytes = 1, ExeMtimeMs = 1 }, Ct).ConfigureAwait(false);
        GameRow d = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _d3dGame, SizeBytes = 1, MtimeUnixMs = 1 }, "D", Ct).ConfigureAwait(false);
        await games.ApplyDetectionAsync(d.Id, new DetectionWrite { CapabilityIds = ["dlss"], RulesVersion = "r", ExeSizeBytes = 1, ExeMtimeMs = 1 }, Ct).ConfigureAwait(false);
        var consent = new FakeConsent();
        var registrar = new FakeRegistrar();
        List<string> log = [];
        return (new VkLayerReconciler(consent, games, registrar, log.Add), consent, registrar, log);
    }

    [Fact]
    public async Task TheLayerIsRegisteredForTheFirstVulkanGameAndUnregisteredAfterTheLast()
    {
        (VkLayerReconciler r, FakeConsent consent, FakeRegistrar reg, List<string> log) = await BuildAsync();

        VkLayerReconcileOutcome none = await r.ReconcileAsync(Ct);
        none.Desired.Should().BeFalse();
        none.Changed.Should().BeFalse();
        reg.Calls.Should().BeEmpty("nothing enabled, nothing registered, nothing to do");

        consent.Enabled.Add(_d3dGame);
        (await r.ReconcileAsync(Ct)).Desired.Should().BeFalse("a hook-enabled game that does not reference the loader is not a Vulkan title");
        reg.Calls.Should().BeEmpty();

        consent.Enabled.Add(_vulkanGame);
        VkLayerReconcileOutcome on = await r.ReconcileAsync(Ct);
        on.Should().Be(new VkLayerReconcileOutcome(Desired: true, Registered: true, Changed: true, VulkanGamesEnabled: 1, Staged: true));
        reg.Calls.Should().Equal("register");
        log.Should().ContainSingle(static l => l.Contains("registered", StringComparison.Ordinal));

        (await r.ReconcileAsync(Ct)).Changed.Should().BeFalse("idempotent: the state already matches");
        reg.Calls.Should().Equal("register");

        consent.Enabled.Remove(_vulkanGame);
        VkLayerReconcileOutcome off = await r.ReconcileAsync(Ct);
        off.Desired.Should().BeFalse();
        off.Registered.Should().BeFalse();
        off.Changed.Should().BeTrue();
        reg.Calls.Should().Equal("register", "unregister");
    }

    [Fact]
    public async Task NothingHappensWhenTheLayerIsNotStaged()
    {
        (VkLayerReconciler r, FakeConsent consent, FakeRegistrar reg, _) = await BuildAsync();
        consent.Enabled.Add(_vulkanGame);
        reg.IsStaged = false;

        VkLayerReconcileOutcome o = await r.ReconcileAsync(Ct);
        o.Desired.Should().BeTrue("the ledger wants it");
        o.Staged.Should().BeFalse();
        o.Changed.Should().BeFalse();
        reg.Calls.Should().BeEmpty("there is no DLL to point a manifest at");
    }

    [Fact]
    public void CapabilityFlagsReadBothShapesAndRefuseGarbage()
    {
        CapabilityFlags.Contains("[\"dlss\",\"vulkan\"]", "vulkan").Should().BeTrue();
        CapabilityFlags.Contains("[\"dlss\"]", "vulkan").Should().BeFalse();
        CapabilityFlags.Contains("{\"vulkan\":true}", "vulkan").Should().BeTrue();
        CapabilityFlags.Contains("{\"vulkan\":false}", "vulkan").Should().BeFalse();
        CapabilityFlags.Contains("not json", "vulkan").Should().BeFalse();
        CapabilityFlags.Contains(null, "vulkan").Should().BeFalse();
    }
}

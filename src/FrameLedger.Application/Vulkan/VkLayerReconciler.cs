using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Vulkan;

/// <summary>
/// The rule <c>12_BUILD</c> §The Vulkan layer is not registered at install time states and nothing enforced until P4
/// PR-2: the layer is registered <b>only while at least one hook-enabled game references the Vulkan loader</b>, and
/// unregistered when the last such game is disabled. One idempotent pass computes the desired state from the
/// ledger — the consent store's enabled games joined to their <c>capability_flags</c> — and moves the registration
/// to it. Called after every consent change the Agent makes and after every detection sweep, so the three ways
/// <c>hook_enabled</c> can drop (revoke, a guard block, the crash policy) and a Vulkan fact that arrives after the
/// consent all converge within a sweep.
/// </summary>
/// <remarks>
/// A game whose flags carry no <c>vulkan</c> id is not a Vulkan title as far as this rule can tell — never scanned
/// yet reads the same as scanned-and-no. That is the fail-safe direction: the layer is machine-wide reach, and
/// a launch reaches it without any registration (<c>VK_ADD_IMPLICIT_LAYER_PATH</c>), so an unregistered layer
/// costs nothing that a launch needs.
/// </remarks>
public sealed class VkLayerReconciler(IGameConsentStore consent, IGameRepository games, IVkLayerRegistrar registrar, Action<string> log)
{
    private readonly IGameConsentStore _consent = consent ?? throw new ArgumentNullException(nameof(consent));
    private readonly IGameRepository _games = games ?? throw new ArgumentNullException(nameof(games));
    private readonly IVkLayerRegistrar _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
    private readonly Action<string> _log = log ?? throw new ArgumentNullException(nameof(log));

    public async ValueTask<VkLayerReconcileOutcome> ReconcileAsync(CancellationToken ct = default)
    {
        int vulkanEnabled = 0;
        foreach (GameConsentRecord record in await _consent.ListEnabledAsync(ct).ConfigureAwait(false))
        {
            GameRow? game = await _games.FindAsync(record.Fingerprint.ExePath, ct).ConfigureAwait(false);
            if (game is not null && CapabilityFlags.Contains(game.CapabilityFlagsJson, StaticDetectionResult.VulkanCapabilityId))
            {
                vulkanEnabled++;
            }
        }

        bool desired = vulkanEnabled > 0;
        if (!_registrar.IsStaged)
        {
            return new VkLayerReconcileOutcome(desired, Registered: false, Changed: false, VulkanGamesEnabled: vulkanEnabled, Staged: false);
        }

        bool registered = _registrar.IsRegistered();
        if (desired == registered)
        {
            return new VkLayerReconcileOutcome(desired, registered, Changed: false, vulkanEnabled, Staged: true);
        }

        if (desired)
        {
            _registrar.Register();
            _log($"vklayer: registered — {vulkanEnabled} hook-enabled game(s) reference the Vulkan loader");
        }
        else
        {
            _registrar.Unregister();
            _log("vklayer: unregistered — no hook-enabled game references the Vulkan loader");
        }

        return new VkLayerReconcileOutcome(desired, desired, Changed: true, vulkanEnabled, Staged: true);
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Vulkan;

/// <summary>
/// The Vulkan layer's per-user registration (<c>17_HOOK_ENGINE</c> §Vulkan: the HKCU <c>ImplicitLayers</c> value),
/// as a port so <see cref="VkLayerReconciler"/> can be tested without a registry. The adapter is
/// <c>Infrastructure.Vulkan.VkLayerRegistrar</c>.
/// </summary>
public interface IVkLayerRegistrar
{
    /// <summary>The layer DLL is beside the Agent; without it there is nothing to register and the reconciler does nothing.</summary>
    bool IsStaged { get; }

    bool IsRegistered();

    /// <summary>Write the manifest for the staged DLL and register it.</summary>
    void Register();

    /// <summary>Remove the registration; true when something was removed.</summary>
    bool Unregister();
}

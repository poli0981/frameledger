// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Vulkan;

/// <summary>What one pass found and did.</summary>
/// <param name="Desired">Whether the ledger says the layer should be registered.</param>
/// <param name="Registered">The registration after the pass.</param>
/// <param name="Changed">Whether the pass registered or unregistered.</param>
/// <param name="VulkanGamesEnabled">Hook-enabled games whose flags carry <c>vulkan</c>.</param>
/// <param name="Staged">Whether the layer DLL is beside the Agent at all.</param>
public sealed record VkLayerReconcileOutcome(bool Desired, bool Registered, bool Changed, int VulkanGamesEnabled, bool Staged);

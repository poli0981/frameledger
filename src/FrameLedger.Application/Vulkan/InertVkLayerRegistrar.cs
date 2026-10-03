// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Vulkan;

/// <summary>
/// The registrar an Agent gets when it does not run over the profile ledger (<c>--data-dir</c>, the test and
/// developer shape — HANDOFF D6/D17): a synthetic ledger's consents must never reach the user's HKCU, so the
/// layer reads as not staged and the reconciler does nothing. The e2e suites grant consent to <c>hook-harness</c>,
/// which references the Vulkan loader, and this is what keeps that grant off the developer's registry.
/// </summary>
public sealed class InertVkLayerRegistrar : IVkLayerRegistrar
{
    public bool IsStaged => false;

    public bool IsRegistered() => false;

    public void Register()
    {
    }

    public bool Unregister() => false;
}

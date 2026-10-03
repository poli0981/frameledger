// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;

namespace FrameLedger.App.Services;

/// <summary>What the App says about itself in <c>Hello</c> and in About.</summary>
internal static class UiIdentity
{
    public static string Version { get; } =
        typeof(UiIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UiIdentity).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}

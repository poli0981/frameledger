// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Settings;

/// <summary>How a <see cref="SettingDefinition"/>'s text value is read and validated.</summary>
public enum SettingKind
{
    /// <summary><c>"1"</c> is true; anything else, or no row, is false — the kill switch's rule (<c>06_DATA_MODEL</c>).</summary>
    Boolean,

    /// <summary>An invariant-culture integer within <see cref="SettingDefinition.Minimum"/>..<see cref="SettingDefinition.Maximum"/>.</summary>
    WholeNumber,

    /// <summary>One of <see cref="SettingDefinition.Choices"/>, compared ordinally.</summary>
    Choice,
}

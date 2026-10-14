// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The edit dialog's "Choose image…" (beta.23, D60), behind an interface so a test can answer it without a dialog.</summary>
public interface IImagePicker
{
    /// <summary>The chosen image's full path, or null when the user cancelled.</summary>
    string? PickImage();
}

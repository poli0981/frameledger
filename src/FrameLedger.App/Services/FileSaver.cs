// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Microsoft.Win32;

namespace FrameLedger.App.Services;

/// <summary>The WPF <see cref="SaveFileDialog"/>.</summary>
public sealed class FileSaver : IFileSaver
{
    public string? PickSavePath(string filter, string suggestedName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = suggestedName, AddExtension = true, OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.App.Services;

/// <summary><see cref="IClipboard"/> over WPF's; the clipboard can be held by another process, which is a refusal, never a crash.</summary>
public sealed class WpfClipboard : IClipboard
{
    public bool SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException ex)
        {
            Serilog.Log.Warning(ex, "ui: the clipboard refused the text");
            return false;
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

internal sealed class NoClipboard(bool accepts = true) : IClipboard
{
    public List<string> Texts { get; } = [];

    public bool SetText(string text)
    {
        Texts.Add(text);
        return accepts;
    }
}

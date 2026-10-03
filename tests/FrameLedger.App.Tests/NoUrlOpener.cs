// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

internal sealed class NoUrlOpener(bool accepts = true) : IUrlOpener
{
    public List<Uri> Opened { get; } = [];

    public bool Open(Uri url)
    {
        Opened.Add(url);
        return accepts;
    }
}

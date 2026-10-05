// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The three answers of <see cref="DataFolderArgument.Read"/>.</summary>
public enum DataFolderChoiceKind
{
    Profile = 0,
    Viewer,
    Refused,
}

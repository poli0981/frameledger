// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>What <see cref="DataFolderArgument.Read"/> decided: the profile, a viewer over <see cref="Folder"/>, or a refusal saying why.</summary>
public sealed record DataFolderChoice(DataFolderChoiceKind Kind, string? Folder, string? Problem)
{
    public static DataFolderChoice Profile { get; } = new(DataFolderChoiceKind.Profile, null, null);

    public static DataFolderChoice Viewer(string folder) => new(DataFolderChoiceKind.Viewer, folder, null);

    public static DataFolderChoice Refused(string problem) => new(DataFolderChoiceKind.Refused, null, problem);
}

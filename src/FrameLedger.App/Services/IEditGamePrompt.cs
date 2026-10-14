// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>FR-1.3's edit dialog as a question: the current metadata in, the edit out, or null when cancelled.</summary>
public interface IEditGamePrompt
{
    /// <summary>Shows the dialog; the edit, or null when the user cancelled.</summary>
    /// <param name="current">What the row holds.</param>
    /// <param name="provenanceJson">Which of its fields detection filled in (badged in the dialog).</param>
    /// <param name="image">The game's image as its page shows it (beta.23, D60); null leaves the dialog without an image row.</param>
    /// <param name="ct">Cancels the dialog.</param>
    Task<GameEdit?> EditAsync(GameMetadata current, string? provenanceJson, EditImage? image = null, CancellationToken ct = default);
}

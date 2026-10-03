// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// One of FR-11's documents as this build ships it: its key (the <c>legal_acceptance.doc</c> value), the title shown, the
/// version the acceptance row records, the text, and the GitHub-hosted copy — since beta.12 at this build's own source
/// (<see cref="RepositoryLinks"/>), with the document's path in the repository (its relative links resolve against it) and
/// whether it is a plain text shown as written rather than Markdown (the GPL's <c>LICENSE</c>).
/// </summary>
public sealed record LegalDocument(string Key, string Title, string Version, string Text, Uri Url, string Path = "", bool IsPlainText = false);

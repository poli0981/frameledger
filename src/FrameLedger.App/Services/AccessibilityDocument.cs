// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>ACCESSIBILITY.md</c> (beta.35, owner decision D88): the project's accessibility statement — what FrameLedger aims
/// for, what works with the keyboard, a screen reader and contrast themes, what does not yet, and how to report a
/// barrier — embedded at build time from the repository root and shown under Help ▸ Accessibility, so the App shows
/// exactly the text this build was reviewed with.
/// </summary>
/// <remarks>
/// <b>Information, not terms</b>, like <see cref="LimitationsDocument"/>: it is not one of FR-11's documents
/// (<see cref="LegalDocuments.Keys"/>), and nothing here is accepted.
/// </remarks>
public static class AccessibilityDocument
{
    /// <summary>The embedded resource's name (<c>FrameLedger.App.csproj</c>).</summary>
    public const string ResourceName = "ACCESSIBILITY.md";

    /// <summary>The same text in the repository at this build's source, for the window's button.</summary>
    public static readonly Uri OnGitHub = RepositoryLinks.Blob(ResourceName);

    /// <summary>The document's text, authoring comments removed.</summary>
    /// <exception cref="InvalidOperationException">The build did not embed it.</exception>
    public static string Load()
    {
        using Stream stream = typeof(AccessibilityDocument).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"the build did not embed {ResourceName}");
        using var reader = new StreamReader(stream);
        return LegalDocuments.StripComments(reader.ReadToEnd());
    }
}

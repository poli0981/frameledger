// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>LIMITATIONS.md</c> (beta.10, owner decision D36) rides in the assembly for Help ▸ Limitations, is information rather
/// than one of FR-11's accepted documents, and every repository link in it names a file that exists.
/// </summary>
public sealed partial class LimitationsDocumentTests
{
    [GeneratedRegex(@"\]\((?<target>[^)#\s]+)(#[^)]*)?\)", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Link();

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "FrameLedger.slnx")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("FrameLedger.slnx not found above the test binary");
        }

        return dir;
    }

    [Fact]
    public void ItIsEmbeddedAsWrittenAndIsNotADocumentTheUserAccepts()
    {
        string text = LimitationsDocument.Load();

        text.Should().StartWith("# What FrameLedger cannot do").And.NotContain("<!--");
        text.Should().Be(LegalDocuments.StripComments(File.ReadAllText(Path.Combine(RepoRoot(), "LIMITATIONS.md"))), "the App shows the file this build was made from");
        LegalDocuments.Keys.Should().NotContain(static k => k.Contains("limit", StringComparison.OrdinalIgnoreCase),
            "the Legal Gate records an acceptance per document it shows; this is information, not terms");
        LimitationsDocument.OnGitHub.Host.Should().Be("github.com");
    }

    [Fact]
    public void EveryRepositoryLinkNamesAFileThatExists()
    {
        string root = RepoRoot();
        string[] targets = [.. Link().Matches(LimitationsDocument.Load()).Select(static m => m.Groups["target"].Value)
            .Where(static t => !t.StartsWith("http", StringComparison.OrdinalIgnoreCase))];

        targets.Should().NotBeEmpty("the page links each limit to the document with its details");
        foreach (string target in targets)
        {
            File.Exists(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue($"LIMITATIONS.md links {target}");
        }
    }
}

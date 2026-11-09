// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>ACCESSIBILITY.md</c> (beta.35, owner decision D88) rides in the assembly for Help ▸ Accessibility, is information rather
/// than one of FR-11's accepted documents, states WCAG 2.2 AA as a goal and never as a claim, sends a barrier to a form that
/// exists, and names only files that exist. The pages a user reads in the App strike nothing through: the App draws struck
/// text as a decoration, so a screen reader reads a withdrawn sentence as if it were current.
/// </summary>
public sealed partial class AccessibilityDocumentTests
{
    [GeneratedRegex(@"\]\((?<target>[^)#\s]+)(#[^)]*)?\)", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Link();

    /// <summary>A sentence that would claim conformance rather than state a goal.</summary>
    [GeneratedRegex(@"\bFrameLedger (?:conforms|complies|meets WCAG|is (?:fully )?(?:accessible|compliant|conformant))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Claim();

    [Fact]
    public void ItIsEmbeddedAsWrittenAndIsNotADocumentTheUserAccepts()
    {
        string text = AccessibilityDocument.Load();

        text.Should().StartWith("# Accessibility").And.NotContain("<!--");
        text.Should().Be(LegalDocuments.StripComments(File.ReadAllText(Path.Combine(AccessibilityTests.RepoRoot(), AccessibilityDocument.ResourceName))),
            "the App shows the file this build was made from");
        LegalDocuments.Keys.Should().NotContain(static k => k.Contains("access", StringComparison.OrdinalIgnoreCase),
            "the Legal Gate records an acceptance per document it shows; this is information, not terms");
        AccessibilityDocument.OnGitHub.Should().Be(new Uri("https://github.com/poli0981/frameledger/blob/main/ACCESSIBILITY.md"));
    }

    [Fact]
    public void EveryRepositoryLinkNamesAFileThatExists()
    {
        string root = AccessibilityTests.RepoRoot();
        string[] targets = [.. Link().Matches(AccessibilityDocument.Load()).Select(static m => m.Groups["target"].Value)
            .Where(static t => !t.StartsWith("http", StringComparison.OrdinalIgnoreCase))];

        targets.Should().NotBeEmpty("the statement links the guide and the developers' details");
        foreach (string target in targets)
        {
            File.Exists(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue($"ACCESSIBILITY.md links {target}");
        }
    }

    /// <summary>D88: WCAG 2.2 AA through WCAG2ICT is a goal; no sentence says FrameLedger conforms, and a barrier has a form.</summary>
    [Fact]
    public void ItStatesAGoalAndNeverAConformanceClaim()
    {
        string text = AccessibilityDocument.Load();

        text.Should().Contain("WCAG 2.2").And.Contain("level AA").And.Contain("WCAG2ICT").And.Contain("not a claim");
        Claim().Matches(text).Select(static m => m.Value).Should().BeEmpty("D88: a goal, never a conformance claim — no evaluation has been made");
        text.Should().Contain("https://github.com/poli0981/frameledger/issues/new?template=accessibility.yml");

        string form = File.ReadAllText(Path.Combine(AccessibilityTests.RepoRoot(), ".github", "ISSUE_TEMPLATE", "accessibility.yml"));
        form.Should().Contain("labels: [accessibility]", "the statement's form labels what it files").And.Contain("name: Accessibility problem");
    }

    /// <summary>
    /// The user guide, LIMITATIONS and this statement are read in the App, where a struck sentence is drawn with a line through
    /// it and read aloud as written. Say what is true now; the history is in the changelog.
    /// </summary>
    [Fact]
    public void NoPageAUserReadsInTheAppStrikesTextThrough()
    {
        IEnumerable<(string Path, string Text)> pages = EmbeddedDocuments.GuidePages.Select(static p => (p, EmbeddedDocuments.Read(p)))
            .Append((LimitationsDocument.ResourceName, LimitationsDocument.Load()))
            .Append((AccessibilityDocument.ResourceName, AccessibilityDocument.Load()));

        pages.Where(static p => OutsideCode(p.Text).Contains("~~", StringComparison.Ordinal)).Select(static p => p.Path)
            .Should().BeEmpty("a screen reader reads struck text as current; rewrite the sentence instead");
    }

    /// <summary>The text outside fenced code blocks.</summary>
    private static string OutsideCode(string markdown) => string.Join('\n', markdown.Split("```").Where(static (_, i) => i % 2 == 0));
}

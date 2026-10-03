// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Markdown;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// Where a link in a shown document leads (beta.12): the browser only for http, https and mailto; a relative path resolved
/// the way GitHub resolves it and never out of the repository; every other scheme refused — a document must not be able to
/// open a local file or start a program. And the repository links of a build that carries no stamped source go to main.
/// </summary>
public sealed class MarkdownLinksTests
{
    [Theory]
    [InlineData("https://github.com/poli0981/frameledger", "https://github.com/poli0981/frameledger")]
    [InlineData("http://example.invalid/a", "http://example.invalid/a")]
    [InlineData("mailto:contact@poli0981.dev", "mailto:contact@poli0981.dev")]
    public void TheBrowserTakesHttpHttpsAndMailto(string href, string expected) =>
        MarkdownLinks.Resolve("legal/EULA.md", href).External.Should().Be(new Uri(expected));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData(@"\\server\share\x.md")]
    [InlineData("//evil.invalid/x")]
    [InlineData("vbscript:msgbox")]
    [InlineData("ms-settings:privacy")]
    [InlineData("../../outside.md")]
    [InlineData("")]
    [InlineData("#")]
    public void EverythingElseIsRefused(string href) => MarkdownLinks.Resolve("legal/EULA.md", href).IsRefused.Should().BeTrue();

    [Theory]
    [InlineData("legal/EULA.md", "PRIVACY_POLICY.md", "legal/PRIVACY_POLICY.md", null)]
    [InlineData("legal/EULA.md", "../LICENSE", "LICENSE", null)]
    [InlineData("legal/EULA.md", "./DISCLAIMER.md#3-no-warranty", "legal/DISCLAIMER.md", "3-no-warranty")]
    [InlineData("README.md", "docs/03_METRICS.md#The-Games-Memory", "docs/03_METRICS.md", "the-games-memory")]
    [InlineData("guide/01-install.md", "/LIMITATIONS.md", "LIMITATIONS.md", null)]
    [InlineData("LIMITATIONS.md", "docs/a%20b.md?plain=1", "docs/a b.md", null)]
    [InlineData(null, "legal/TRADEMARKS.md", "legal/TRADEMARKS.md", null)]
    public void ARelativePathResolvesAgainstTheDocumentsFolder(string? document, string href, string path, string? anchor)
    {
        MarkdownLinkTarget target = MarkdownLinks.Resolve(document, href);

        target.RepositoryPath.Should().Be(path);
        target.Anchor.Should().Be(anchor);
        target.External.Should().BeNull();
    }

    [Fact]
    public void AnAnchorAloneStaysInTheDocument()
    {
        MarkdownLinkTarget target = MarkdownLinks.Resolve("LIMITATIONS.md", "#The-Games-Memory");

        target.Anchor.Should().Be("the-games-memory");
        target.RepositoryPath.Should().BeNull();
    }

    [Theory]
    [InlineData("The game's memory", "the-games-memory")]
    [InlineData("1. What it observes (Tier 1)", "1-what-it-observes-tier-1")]
    [InlineData("  FPS & frame generation  ", "fps--frame-generation")]
    [InlineData("snake_case-and-dash", "snake_case-and-dash")]
    public void HeadingsAreSluggedTheWayGitHubSlugsThem(string heading, string slug) => MarkdownLinks.Slug(heading).Should().Be(slug);

    [Fact]
    public void ABuildWithNoStampedSourceLinksToMainAndEscapesThePath()
    {
        RepositoryLinks.SourceRef.Should().Be("main", "a test build carries no FrameLedgerSourceRef");
        RepositoryLinks.Blob("docs/a b.md", "x y").Should().Be(new Uri("https://github.com/poli0981/frameledger/blob/main/docs/a%20b.md#x%20y"));
        RepositoryLinks.Tree(string.Empty).Should().Be(new Uri("https://github.com/poli0981/frameledger/tree/main"));
    }

    [Theory]
    [InlineData("v0.1.0-beta.12", "v0.1.0-beta.12")]
    [InlineData("2ce825109f6a7b3c1d2e4f5a6b7c8d9e0f1a2b3c", "2ce825109f6a7b3c1d2e4f5a6b7c8d9e0f1a2b3c")]
    [InlineData(null, "main")]
    [InlineData("", "main")]
    [InlineData("../evil", "main")]
    [InlineData("v1..2", "main")]
    [InlineData("a/b", "main")]
    [InlineData("tag?x=1", "main")]
    [InlineData("-option", "main")]
    public void OnlyAPlainRefIsTakenFromTheBuild(string? stamped, string used) => RepositoryLinks.Validate(stamped).Should().Be(used);
}

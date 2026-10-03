// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>FR-11's four documents ride in the assembly, each with the version the acceptance row will record, and only the one placeholder 12_BUILD allows.</summary>
public sealed class LegalDocumentsTests
{
    [Fact]
    public void TheFourDocumentsLoadWithTheirVersions()
    {
        IReadOnlyList<LegalDocument> docs = LegalDocuments.Load();

        docs.Select(static d => d.Key).Should().Equal(LegalDocuments.Keys);
        docs.Should().OnlyContain(static d => d.Text.Length > 200);
        docs.Should().OnlyContain(static d => d.Version.Length > 0);
        docs.Single(static d => string.Equals(d.Key, LegalDocuments.Gpl, StringComparison.Ordinal)).Version.Should().Be(LegalDocuments.GplVersion);
        docs.Single(static d => string.Equals(d.Key, LegalDocuments.Eula, StringComparison.Ordinal)).Version.Should().MatchRegex(@"^\d+\.\d+");
        docs.Should().OnlyContain(static d => d.Url.Host == "github.com");

        // beta.12: at this build's source (main for a test build), with the path the document's relative links resolve against;
        // the GPL is a plain text shown as written.
        docs.Select(static d => d.Path).Should().Equal("legal/EULA.md", "LICENSE", "legal/DISCLAIMER.md", "legal/PRIVACY_POLICY.md");
        docs.Select(static d => d.Url.AbsoluteUri).Should().OnlyContain(static u => u.StartsWith("https://github.com/poli0981/frameledger/blob/main/", StringComparison.Ordinal));
        docs.Where(static d => d.IsPlainText).Select(static d => d.Key).Should().Equal(LegalDocuments.Gpl);
    }

    [Fact]
    public void AuthoringCommentsAreStrippedAndTheOnlySurvivingPlaceholderIsTheReleaseDate()
    {
        foreach (LegalDocument d in LegalDocuments.Load())
        {
            d.Text.Should().NotContain("<!--", d.Key);
            d.Text.Replace("{{RELEASE_DATE}}", string.Empty, StringComparison.Ordinal).Should().NotContain("{{", "12_BUILD §Release-time token substitution");
        }
    }

    [Fact]
    public void TheVersionLineIsTheDocumentsOwn()
    {
        LegalDocuments.VersionOf("# X\n\n**Version:** 2.0-draft · **Effective:** {{RELEASE_DATE}}\n").Should().Be("2.0-draft");
        LegalDocuments.VersionOf("no version here").Should().BeNull();
        LegalDocuments.StripComments("a <!-- b\nc --> d").Should().Be("a  d");
    }
}

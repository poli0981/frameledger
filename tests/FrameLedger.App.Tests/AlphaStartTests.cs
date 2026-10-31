// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared;

namespace FrameLedger.App.Tests;

/// <summary>
/// What an alpha does at the App's start (D73, D74): the ledger copy it asks for — none on the main line, none in a viewer —
/// and the Agent held while its Legal Gate waits for the Alpha notice, released only by the Accept.
/// </summary>
public sealed class AlphaStartTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IReadOnlyList<LegalDocument> AlphaDocs { get; } =
    [
        new("eula", "EULA", "1.0", "text", new Uri("https://github.com/x/eula")),
        new(LegalDocuments.Alpha, "Alpha notice", AlphaNotice.Version, "text", new Uri("https://github.com/x/a")),
    ];

    [Fact]
    public void OnlyAnAlphaOverItsOwnFolderAsksForTheLedgerCopy()
    {
        string database = Path.Combine(Path.GetTempPath(), "fl-alpha-start", LedgerPaths.DatabaseFileName);

        AlphaStart.BackupFor(BuildLine.Main, viewer: false, database, "0.1.0-beta.32").Should().BeNull("the main line copies nothing");
        AlphaStart.BackupFor(BuildLine.Alpha, viewer: true, database, "0.1.0-alpha.1").Should().BeNull("a viewer reads a copy already");

        LedgerBackupRequest? request = AlphaStart.BackupFor(BuildLine.Alpha, viewer: false, database, "0.1.0-alpha.1");
        request.Should().NotBeNull();
        request!.Directory.Should().Be(LedgerPaths.BackupsDirectoryOf(database));
        request.Version.Should().Be("0.1.0-alpha.1");
        request.Keep.Should().Be(3);
    }

    [Fact]
    public async Task AnAlphaWithTheNoticeOutstandingHoldsTheAgent()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var link = new FakeAgentLink();

        (await AlphaStart.HoldTheAgentForTheNoticeAsync(BuildLine.Alpha, new LegalGate(new SqliteLegalAcceptanceStore(s.Db), AlphaDocs), link, Ct)).Should().BeTrue();

        link.NoticeHolds.Should().Equal([true]);
    }

    [Fact]
    public async Task AnAcceptedNoticeOrTheMainLineHoldsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(s.Db);
        var gate = new LegalGate(store, AlphaDocs);
        var link = new FakeAgentLink();

        (await AlphaStart.HoldTheAgentForTheNoticeAsync(BuildLine.Main, gate, link, Ct)).Should().BeFalse("the main line's Agent never waits for a notice");

        await store.RecordAsync(new Application.Persistence.LegalAcceptance(LegalDocuments.Alpha, AlphaNotice.Version, DateTimeOffset.UnixEpoch), Ct);
        (await AlphaStart.HoldTheAgentForTheNoticeAsync(BuildLine.Alpha, gate, link, Ct)).Should().BeFalse("the notice is accepted; only the EULA is outstanding");

        link.NoticeHolds.Should().BeEmpty();
    }

    [Fact]
    public void ALinkOrAWindowSetThatHasNothingToHoldOrShowAcceptsTheCall()
    {
        IAgentLink viewer = new ViewerAgentLink();
        IDocumentWindows none = new NoDocuments();

        viewer.Invoking(static v => v.SetNoticeHold(true)).Should().NotThrow("a viewer starts no Agent, so there is nothing to hold");
        none.Invoking(static d => d.ShowAlphaNotice()).Should().NotThrow();
    }

    private sealed class NoDocuments : IDocumentWindows
    {
        public void ShowGuide()
        {
        }

        public void ShowLimitations()
        {
        }

        public void ShowAbout()
        {
        }
    }
}

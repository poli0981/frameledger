// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Import;
using FrameLedger.Infrastructure.Watch;

namespace FrameLedger.App.Tests;

/// <summary>
/// File ▸ Import library… end to end over a scratch ledger and real files (P4 PR-4): a store's titles reach the
/// review, the ticked ones land as rows with hooking off and the store's facts, the Games page is shown; nothing found
/// or a cancel adds nothing. And the review's own bookkeeping: the count follows the ticks, select-all skips what
/// cannot be imported.
/// </summary>
public sealed class ImportLibraryFlowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-import-" + Guid.NewGuid().ToString("N"));

    public ImportLibraryFlowTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ScriptedStore(params StoreGame[] games) : IStoreLibrarySource
    {
        public string Platform => "epic";

        public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    private sealed class ScriptedReview(Func<IReadOnlyList<ImportCandidate>, IReadOnlyList<ImportCandidate>?> answer) : IImportReview
    {
        public List<IReadOnlyList<ImportCandidate>> Shown { get; } = [];

        public Task<IReadOnlyList<ImportCandidate>?> ReviewAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default)
        {
            Shown.Add(candidates);
            return Task.FromResult(answer(candidates));
        }
    }

    private sealed class RecordingStrip : IMessageStrip
    {
        public List<string> Kinds { get; } = [];

        public void Info(string title, string body) => Kinds.Add("info");

        public void Success(string title, string body) => Kinds.Add("success");

        public void Warn(string title, string body) => Kinds.Add("warn");
    }

    private sealed class FakeNavigator : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack() => Pages.Add("back");
    }

    private async Task<string> ExeAsync(string name)
    {
        string dir = Path.Combine(_dir, name);
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, name + ".exe");
        await File.WriteAllBytesAsync(exe, new byte[64], Ct).ConfigureAwait(false);
        return exe;
    }

    [Fact]
    public async Task TheTickedTitlesLandWithHookingOffAndTheStoresFacts()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string alan = await ExeAsync("AlanWake2");
        string fort = await ExeAsync("Fortnite");
        var store = new ScriptedStore(
            new StoreGame("epic", "aw2", "Alan Wake 2", Path.GetDirectoryName(alan)!, alan, "1.2.3"),
            new StoreGame("epic", "fn", "Fortnite", Path.GetDirectoryName(fort)!, fort, null),
            new StoreGame("epic", "gone", "Uninstalled", Path.Combine(_dir, "gone"), Path.Combine(_dir, "gone", "gone.exe"), null));
        var review = new ScriptedReview(static c => [.. c.Where(static x => string.Equals(x.Game.StoreId, "aw2", StringComparison.Ordinal))]);
        var strip = new RecordingStrip();
        var nav = new FakeNavigator();
        var importer = new LibraryImporter([store], s.Games, new ExecutableIdentitySource(), new ExecutableLocator(), static _ => { });
        var flow = new ImportLibraryFlow(importer, review, strip, nav);

        ImportReport? report = await flow.RunAsync(ct: Ct);

        report.Should().NotBeNull();
        report!.Added.Should().Be(1);
        review.Shown.Should().ContainSingle().Which.Should().HaveCount(3, "every title the store named reaches the review, importable or not");
        review.Shown[0].Single(static c => string.Equals(c.Game.StoreId, "gone", StringComparison.Ordinal)).CanImport.Should().BeFalse("its executable is not on disk");
        GameRow row = (await s.Games.ListAsync(Ct)).Should().ContainSingle().Subject;
        row.Name.Should().Be("Alan Wake 2");
        row.HookEnabled.Should().BeFalse("FR-1.2: import never enables hooking");
        row.Platform.Should().Be("epic");
        row.StoreId.Should().Be("aw2");
        row.GameVersion.Should().Be("1.2.3");
        strip.Kinds.Should().Equal("success");
        nav.Pages.Should().Equal(nameof(GamesPage));
    }

    [Fact]
    public async Task NothingFoundOrACancelAddsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var strip = new RecordingStrip();
        var nav = new FakeNavigator();
        var review = new ScriptedReview(static _ => null);
        var empty = new ImportLibraryFlow(new LibraryImporter([new ScriptedStore()], s.Games, new ExecutableIdentitySource(), new ExecutableLocator(), static _ => { }), review, strip, nav);
        (await empty.RunAsync(ct: Ct)).Should().BeNull();
        strip.Kinds.Should().Equal("info");
        review.Shown.Should().BeEmpty("nothing to review");

        string exe = await ExeAsync("Game");
        var cancelled = new ImportLibraryFlow(new LibraryImporter([new ScriptedStore(new StoreGame("epic", "g", "Game", Path.GetDirectoryName(exe)!, exe, null))], s.Games, new ExecutableIdentitySource(), new ExecutableLocator(), static _ => { }), review, strip, nav);
        (await cancelled.RunAsync(ct: Ct)).Should().BeNull();
        (await s.Games.ListAsync(Ct)).Should().BeEmpty();
        nav.Pages.Should().BeEmpty();
    }

    /// <summary>
    /// beta.11: the Games page's Refresh runs the import for new games only — every title already in the library means "no
    /// new games" and no checklist; a new one opens it, and the page on screen is told the library changed.
    /// </summary>
    [Fact]
    public async Task RefreshOffersOnlyNewGamesAndSaysWhenThereAreNone()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string old = await ExeAsync("Old");
        await s.Games.EnsureAsync(new ExecutableIdentitySource().Read(old)!.Value, "Old", Ct);
        var oldStore = new ScriptedStore(new StoreGame("epic", "old", "Old", Path.GetDirectoryName(old)!, old, null));
        var review = new ScriptedReview(static c => [.. c.Where(static x => x.CanImport)]);
        var strip = new RecordingStrip();
        var nav = new FakeNavigator();
        var changes = new LibraryChanges();
        int announced = 0;
        changes.Changed += (_, _) => announced++;
        var flow = new ImportLibraryFlow(new LibraryImporter([oldStore], s.Games, new ExecutableIdentitySource(), new ExecutableLocator(), static _ => { }),
            review, strip, nav, changes);

        (await flow.RunAsync(onlyNew: true, Ct)).Should().BeNull();
        review.Shown.Should().BeEmpty("nothing new, so no checklist");
        strip.Kinds.Should().Equal("info");
        announced.Should().Be(0);

        string fresh = await ExeAsync("Fresh");
        var both = new ScriptedStore(new StoreGame("epic", "old", "Old", Path.GetDirectoryName(old)!, old, null),
            new StoreGame("epic", "fresh", "Fresh", Path.GetDirectoryName(fresh)!, fresh, null));
        flow = new ImportLibraryFlow(new LibraryImporter([both], s.Games, new ExecutableIdentitySource(), new ExecutableLocator(), static _ => { }),
            review, strip, nav, changes);

        ImportReport? report = await flow.RunAsync(onlyNew: true, Ct);

        report!.Added.Should().Be(1);
        announced.Should().Be(1, "the Games page on screen reloads");
        nav.Pages.Should().Equal(nameof(GamesPage));
    }

    [Fact]
    public void TheReviewCountsTicksAndSelectAllSkipsWhatCannotBeImported()
    {
        var can = new ImportCandidate(new StoreGame("steam", "1", "A", @"C:\a", @"C:\a\a.exe", null), @"C:\a\a.exe", AlreadyInLibrary: false, ExecutableGuessed: true);
        var already = new ImportCandidate(new StoreGame("steam", "2", "B", @"C:\b", @"C:\b\b.exe", null), @"C:\b\b.exe", AlreadyInLibrary: true, ExecutableGuessed: false);
        var none = new ImportCandidate(new StoreGame("steam", "3", "C", @"C:\c", null, null), null, AlreadyInLibrary: false, ExecutableGuessed: true);
        var moved = new ImportCandidate(new StoreGame("steam", "4", "D", @"D:\d", @"D:\d\d.exe", null), @"D:\d\d.exe", AlreadyInLibrary: false, ExecutableGuessed: false,
            MovedFrom: @"H:\d\d.exe");
        var vm = new ImportReviewViewModel([can, already, none, moved]);

        vm.SelectedCount.Should().Be(1, "only the importable row is ticked by default");
        vm.CanImport.Should().BeTrue();
        vm.Rows[1].Note.Should().Be(Strings.Import_Note_Already);
        vm.Rows[2].Note.Should().Be(Strings.Import_Note_NoExe);
        vm.Rows[0].Note.Should().Be(Strings.Import_Note_Guessed);
        vm.Rows[3].Note.Should().Be(Strings.Import_Note_Moved, "in the library under another drive letter (2026-09-23): that entry follows the file");
        vm.Rows[3].CanImport.Should().BeFalse();

        vm.SelectNoneCommand.Execute(null);
        vm.SelectedCount.Should().Be(0);
        vm.CanImport.Should().BeFalse();
        vm.SelectAllCommand.Execute(null);
        vm.SelectedCount.Should().Be(1);
        vm.Selected.Should().ContainSingle().Which.Should().Be(can);
    }

    /// <summary>
    /// beta.11 (owner request 2026-10-03): the games already in the library — at this path or under another drive letter —
    /// are hidden by default and come back, still unimportable, when the user asks; the count names how many.
    /// </summary>
    [Fact]
    public void GamesAlreadyInTheLibraryAreHiddenUntilAskedFor()
    {
        var can = new ImportCandidate(new StoreGame("steam", "1", "A", @"C:\a", @"C:\a\a.exe", null), @"C:\a\a.exe", AlreadyInLibrary: false, ExecutableGuessed: false);
        var already = new ImportCandidate(new StoreGame("steam", "2", "B", @"C:\b", @"C:\b\b.exe", null), @"C:\b\b.exe", AlreadyInLibrary: true, ExecutableGuessed: false);
        var none = new ImportCandidate(new StoreGame("steam", "3", "C", @"C:\c", null, null), null, AlreadyInLibrary: false, ExecutableGuessed: true);
        var moved = new ImportCandidate(new StoreGame("steam", "4", "D", @"D:\d", @"D:\d\d.exe", null), @"D:\d\d.exe", AlreadyInLibrary: false, ExecutableGuessed: false,
            MovedFrom: @"H:\d\d.exe");
        var vm = new ImportReviewViewModel([can, already, none, moved]);

        vm.ExistingCount.Should().Be(2);
        vm.HasExisting.Should().BeTrue();
        vm.ShowExistingText.Should().Contain("2");
        vm.Shown.Select(static r => r.Name).Should().Equal(["A", "C"], "a title with no executable is new and says why it cannot be imported");

        vm.ShowExisting = true;
        vm.Shown.Select(static r => r.Name).Should().Equal(["A", "B", "C", "D"]);
        vm.Shown.Where(static r => r.IsExisting).Should().OnlyContain(static r => !r.CanImport, "shown dimmed, never importable");

        vm.SelectAllCommand.Execute(null);
        vm.SelectedCount.Should().Be(1, "showing them makes them no more importable");
        vm.ShowExisting = false;
        vm.Shown.Should().HaveCount(2);
    }
}

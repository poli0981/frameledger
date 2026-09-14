using System.Windows;
using FluentAssertions;
using FrameLedger.App.Controls;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Metrics;
using FrameLedger.Shared.Ipc;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// Every page's XAML and the two control templates load under the real theme dictionaries — the failure a
/// binding typo or a missing resource key produces is a XamlParseException at first navigation, which no
/// view-model test sees. Runs on an STA thread with an <c>Application</c> of its own.
/// </summary>
public sealed class PagesLoadTests
{
    private sealed class NoAgent : IAgentLink
    {
        public AgentConnectionState State => AgentConnectionState.Missing;

        public StatusAck? Status => null;

        public HelloAck? Hello => null;

        public bool IsConnected => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<AgentEventArgs>? EventReceived
        {
            add { }
            remove { }
        }

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class => throw new NotSupportedException();
    }

    private sealed class NoNavigation : IPageNavigator
    {
        public void Navigate<TPage>()
            where TPage : class
        {
        }

        public void GoBack()
        {
        }
    }

    private sealed class NoPrompt : IConsentPrompt
    {
        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class NoConfirm : IConfirmations
    {
        public Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default) => Task.FromResult(RemoveGameChoice.Cancel);
    }

    private sealed class NoEdit : IEditGamePrompt
    {
        public Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default) => Task.FromResult<GameMetadata?>(null);
    }

    private sealed class NoStrip : IMessageStrip
    {
        public void Info(string title, string body)
        {
        }

        public void Success(string title, string body)
        {
        }

        public void Warn(string title, string body)
        {
        }
    }

    private sealed class NoPicker : IGamePicker
    {
        public string? PickExecutable() => null;
    }

    private static Task<T> OnStaAsync<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
                    app.Resources.MergedDictionaries.Add(new ControlsDictionary());
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/FrameLedger;component/Styles/FrameLedger.xaml") });
                }

                tcs.SetResult(work());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    private static void Render(FrameworkElement element)
    {
        element.Measure(new Size(1200, 900));
        element.Arrange(new Rect(0, 0, 1200, 900));
        element.UpdateLayout();
    }

    [Fact]
    public async Task EveryPageLoadsUnderTheThemeDictionaries()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-3), hooked: false);
        var selection = new GameSelection { GameId = game.Id };
        var nav = new NoNavigation();
        var strip = new NoStrip();
        var addGame = new AddGameFlow(s.Library, new NoPicker(), selection, nav, strip);
        var consent = new HookingConsent(new NoAgent(), new NoPrompt());

        // The view models load off the STA thread; the pages bind to them on it.
        using var dashboard = new DashboardViewModel(new NoAgent(), s.Library, selection, nav);
        Task pending1 = dashboard.Pending;
        await pending1;
        var games = new GamesViewModel(s.Library, selection, nav, addGame);
        Task pending2 = games.Pending;
        await pending2;
        var detail = new GameDetailViewModel(s.Library, selection, consent, nav, new NoConfirm(), new NoEdit(), strip);
        Task pending3 = detail.Pending;
        await pending3;

        string loaded = await OnStaAsync(() =>
        {
            Render(new DashboardPage(dashboard));
            Render(new GamesPage(games));
            Render(new GameDetailPage(detail));
            var readout = new FpsReadout { Model = FpsPresentation.FromRow(detail.Sessions[0].Row) };
            Render(readout);
            var chip = new TriStateChip { Model = new TriStateChipModel("RT", Tri.Yes, Application.TriState.TriStateSource.Measured) };
            Render(chip);
            return "dashboard,games,detail,readout,chip";
        });

        loaded.Should().Be("dashboard,games,detail,readout,chip");
        games.Games.Should().ContainSingle();
        detail.Sessions.Should().HaveCount(2);
        detail.Sessions.Should().Contain(static r => r.TierText == Strings.Tier_NotHooked);
    }
}

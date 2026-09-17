using System.Windows.Threading;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Tests.Update;
using FrameLedger.App.Update;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Settings;

namespace FrameLedger.App.Tests;

/// <summary>
/// The installed 0.1.0-beta.1 (2026-09-17) closed with a Fatal line and exit code 1: the Generic Host stopped its services on
/// a thread-pool thread and <see cref="TrayHost.Dispose"/> removed the icon there — a WPF object whose disposal unsubscribes
/// <c>Application.Exit</c>, both owned by the UI thread.
/// </summary>
public sealed class TrayHostTests
{
    private sealed class Shell : IShellPresence
    {
        public bool IsShown => true;

        public void Reveal()
        {
        }

        public void Quit()
        {
        }
    }

    private sealed class Summaries : ISessionSummaryOpener
    {
        public void Open(long sessionId)
        {
        }
    }

    private sealed class Navigation : IPageNavigator
    {
        public void Navigate<TPage>()
            where TPage : class
        {
        }

        public void GoBack()
        {
        }
    }

    [Fact]
    public async Task DisposingTheTrayFromAPoolThreadRemovesTheIconOnTheUiThread()
    {
        (bool created, Exception? error, bool stillCreated) = await PagesLoadTests.OnStaAsync(static () =>
        {
            var link = new FakeAgentLink();
            using var updates = new UpdateService(new FakeUpdateClient(), link, new RegisteredSettings(new MemorySettings()), new FakeUpdatePrompts(), new Shell(), new RecordingStrip());
            using var viewModel = new TrayViewModel(link, new Shell(), new Summaries(), new Navigation(), updates);
            var tray = new TrayHost(viewModel, new WindowClosePolicy());
            tray.Create();
            bool created = tray.IsCreated;

            // The host's shape: this (UI) thread keeps pumping while a pool thread stops the services.
            Exception? error = null;
            var frame = new DispatcherFrame();
            _ = Task.Run(() =>
            {
                try
                {
                    tray.Dispose();
                }
                catch (InvalidOperationException ex)
                {
                    error = ex;
                }
                finally
                {
                    frame.Continue = false;
                }
            });
            Dispatcher.PushFrame(frame);
            return (created, error, tray.IsCreated);
        }).ConfigureAwait(true);

        Assert.SkipUnless(created, "no notification area in this session (Shell_NotifyIcon refused), so there is no icon to remove");
        error.Should().BeNull("the icon is removed on the thread that owns it, whatever thread asks");
        stillCreated.Should().BeFalse();
    }
}

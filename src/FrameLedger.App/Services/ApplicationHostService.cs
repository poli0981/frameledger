using System.Windows;
using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>
/// Shows the shell once the host is up (the WPF UI template's idiom, under our own <see cref="ShellHost"/>), then
/// the tray (P3 PR-8b) — after FR-11's gate when it is required (P3 PR-9): a first run, or a document whose version
/// moved. A decline ends the host; the shell never appears.
/// </summary>
internal sealed class ApplicationHostService(ShellHost shell, TrayHost tray, IFirstRunFlow firstRun, IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (System.Windows.Application.Current.Windows.OfType<MainWindow>().Any())
        {
            tray.Create();
            return;
        }

        if (await firstRun.IsRequiredAsync(cancellationToken).ConfigureAwait(true))
        {
            // Not awaited inside StartAsync: the host must finish starting (the Agent connection runs meanwhile,
            // which is what step 2 shows); the continuation shows the shell or stops the host.
            _ = ContinueAsync();
            return;
        }

        shell.Show();
        tray.Create();
    }

    private async Task ContinueAsync()
    {
        try
        {
            if (await firstRun.RunAsync().ConfigureAwait(true))
            {
                shell.Show();
                tray.Create();
            }
            else
            {
                lifetime.StopApplication();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Fatal(ex, "ui: the first-run flow failed");
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        tray.Dispose();
        return Task.CompletedTask;
    }
}

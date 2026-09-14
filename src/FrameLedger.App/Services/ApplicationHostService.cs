using System.Windows;
using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>Shows the shell once the host is up (the WPF UI template's idiom, under our own <see cref="ShellHost"/>), then the tray (P3 PR-8b).</summary>
internal sealed class ApplicationHostService(ShellHost shell, TrayHost tray) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!System.Windows.Application.Current.Windows.OfType<MainWindow>().Any())
        {
            shell.Show();
        }

        tray.Create();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        tray.Dispose();
        return Task.CompletedTask;
    }
}

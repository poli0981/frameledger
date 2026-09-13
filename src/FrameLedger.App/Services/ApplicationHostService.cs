using System.Windows;
using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>Shows the shell once the host is up (the WPF UI template's idiom, under our own <see cref="ShellHost"/>).</summary>
internal sealed class ApplicationHostService(ShellHost shell) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (System.Windows.Application.Current.Windows.OfType<MainWindow>().Any())
        {
            return Task.CompletedTask;
        }

        shell.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

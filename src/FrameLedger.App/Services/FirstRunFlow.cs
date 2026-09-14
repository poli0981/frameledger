using FrameLedger.App.ViewModels;
using FrameLedger.App.Windows;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary><see cref="IFirstRunFlow"/> over <see cref="FirstRunWindow"/>: one window per run, closed when the view model decides.</summary>
public sealed class FirstRunFlow : IFirstRunFlow
{
    private readonly LegalGate _gate;
    private readonly IAgentLink _agent;
    private readonly IThemeApplier _theme;
    private readonly AppearanceSettings _appearance;
    private readonly ShellHost _shell;

    public FirstRunFlow(LegalGate gate, IAgentLink agent, IThemeApplier theme, AppearanceSettings appearance, ShellHost shell)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public Task<bool> IsRequiredAsync(CancellationToken ct = default) => _gate.IsRequiredAsync(ct);

    public Task<bool> RunAsync(CancellationToken ct = default) => ShowAsync(readOnly: false);

    public Task ShowDocumentsAsync(CancellationToken ct = default) => ShowAsync(readOnly: true);

    private async Task<bool> ShowAsync(bool readOnly)
    {
        using var viewModel = new FirstRunViewModel(_gate, _agent, readOnly);
        var window = new FirstRunWindow(viewModel, _theme, _appearance) { Owner = readOnly ? _shell.Current : null };
        window.Show();
        Log.Information("ui: first-run window shown {Ms} ms after process start", (long)(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds);
        Task<bool> outcome = viewModel.Outcome;
        bool accepted = await outcome.ConfigureAwait(true);
        if (window.IsVisible)
        {
            window.Close();
        }

        Log.Information("first run: {Outcome} (read-only {ReadOnly})", accepted ? "accepted" : "declined", readOnly);
        return accepted;
    }
}

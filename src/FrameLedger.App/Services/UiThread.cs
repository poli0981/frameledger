using System.Diagnostics.CodeAnalysis;

namespace FrameLedger.App.Services;

/// <summary>
/// The thread a view model was built on, captured as its <see cref="SynchronizationContext"/>: the pipe's
/// events arrive on the thread pool, and property changes have to land on the dispatcher. Under WPF the
/// context is the dispatcher's; in a test without one the action runs inline, so a view model can be exercised
/// with no <c>Application</c> at all.
/// </summary>
public sealed class UiThread
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; posting to the captured context is the documented way to reach the dispatcher from the thread pool, and it never blocks")]
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_context is null)
        {
            action();
            return;
        }

        _context.Post(static state => ((Action)state!)(), action);
    }
}

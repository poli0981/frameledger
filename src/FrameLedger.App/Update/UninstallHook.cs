namespace FrameLedger.App.Update;

/// <summary>
/// The uninstall hook's decisions (<c>12_BUILD</c> §Publish &amp; package: "uninstalled → unregister the layer, remove
/// the scheduled task, ask about the data folder"), over delegates so a test runs it without a pipe, a registry, a
/// Task Scheduler or a data folder. Every step is independent: a failure in one is logged and the next still
/// runs, and the data folder is deleted only on an explicit yes — a question that goes unanswered inside
/// Velopack's 30 s budget leaves the folder where it is.
/// </summary>
public sealed class UninstallHook(
    Func<bool> stopAgent,
    Func<bool> unregisterLayer,
    Func<bool> removeTask,
    Func<bool> askDeleteData,
    Action deleteData,
    Action<string> log)
{
    public UninstallOutcome Run()
    {
        bool agent = Step("agent shutdown", stopAgent);
        bool layer = Step("vulkan layer unregister", unregisterLayer);
        bool task = Step("logon task remove", removeTask);
        bool asked = false;
        bool deleted = false;
        if (Step("data folder question", () =>
        {
            asked = true;
            return askDeleteData();
        }))
        {
            deleted = Step("data folder delete", () =>
            {
                deleteData();
                return true;
            });
        }

        var outcome = new UninstallOutcome(agent, layer, task, asked, deleted);
        log("uninstall: " + outcome);
        return outcome;
    }

    /// <summary>
    /// True when neither directory contains the other. The data folder is deleted only then: an install living inside
    /// it (a package id equal to the data folder's name — Velopack installs to <c>%LOCALAPPDATA%\&lt;packId&gt;</c>) would
    /// have the delete remove the running install, and the question could not honestly be asked.
    /// </summary>
    public static bool AreSeparate(string dataDirectory, string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        string data = Normalise(dataDirectory);
        string install = Normalise(installDirectory);
        return !data.StartsWith(install, StringComparison.OrdinalIgnoreCase) && !install.StartsWith(data, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalise(string directory) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(directory)) + System.IO.Path.DirectorySeparatorChar;

    private bool Step(string name, Func<bool> action)
    {
        try
        {
            bool done = action();
            log($"uninstall: {name}: {(done ? "done" : "nothing to do")}");
            return done;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log($"uninstall: {name} failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}

namespace FrameLedger.App.Services;

/// <summary>
/// <c>07_IPC</c> §Client behavior: "on failure start the Agent, retry". The Agent is the one beside this
/// executable — <c>12_BUILD</c> publishes both roots into one directory — and nothing else; the App never
/// searches for one.
/// </summary>
public interface IAgentLauncher
{
    /// <summary>Whether an Agent executable is beside this one at all.</summary>
    bool CanLaunch { get; }

    /// <summary>Start <c>FrameLedger.Agent.exe --serve</c>, detached; false when it could not be started.</summary>
    bool TryStart();
}

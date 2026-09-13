namespace FrameLedger.App.Services;

/// <summary>What the status pill and the banner show (08_UI §Shell: "Agent state shown as a pill").</summary>
public enum AgentConnectionState
{
    /// <summary>A connect round is in progress.</summary>
    Connecting = 0,

    /// <summary>No Agent answered; it was started from beside this executable and a new round runs.</summary>
    Starting,

    /// <summary><c>Hello</c> answered.</summary>
    Connected,

    /// <summary>An Agent exists beside this executable and is not answering; rounds continue.</summary>
    Offline,

    /// <summary>No Agent executable is beside this one; nothing to start, rounds continue in case one appears.</summary>
    Missing,
}

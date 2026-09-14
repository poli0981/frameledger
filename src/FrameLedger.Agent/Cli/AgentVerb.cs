namespace FrameLedger.Agent.Cli;

/// <summary>What the Agent can be asked to do (P2 PR-F). The surface is pinned by <c>AgentCommandLineSurfaceTests</c>.</summary>
internal enum AgentVerb
{
    None = 0,

    /// <summary><c>--serve</c>: the watcher-driven host, the product's mode.</summary>
    Serve,

    /// <summary>A flag <c>12_BUILD</c> §Debugging lists that P2 does not implement; exit 2 with the reason.</summary>
    NotImplemented,

    ConsentList,
    ConsentGrant,
    ConsentRevoke,
    Capture,
    Launch,
    Recover,
    Sessions,
    DbPath,
    GamesAdd,
    KillSwitchOn,
    KillSwitchOff,
    KillSwitchStatus,

    /// <summary><c>--register-vklayer</c> (P3 PR-8b): the HKCU registration, a repair tool (<c>12_BUILD</c> §The Vulkan layer is not registered at install time).</summary>
    RegisterVkLayer,

    /// <summary><c>--unregister-vklayer</c>.</summary>
    UnregisterVkLayer,

    /// <summary><c>--install-task</c>: the logon scheduled task for <c>--serve</c>, this user, lowest privileges.</summary>
    InstallTask,

    /// <summary><c>--uninstall-task</c>.</summary>
    UninstallTask,
}

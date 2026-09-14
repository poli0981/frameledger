namespace FrameLedger.App.Update;

/// <summary>What the uninstall hook did, for its log line and its test.</summary>
public sealed record UninstallOutcome(bool AgentAsked, bool LayerUnregistered, bool TaskRemoved, bool DataAsked, bool DataDeleted);

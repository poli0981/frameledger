namespace FrameLedger.App.Services;

/// <summary>The per-user Run entry behind an interface so the settings page is testable without touching the registry.</summary>
public interface IRunAtLogon
{
    bool IsSet { get; }

    void Apply(bool enabled);
}

namespace FrameLedger.App.Services;

/// <summary>The 4 s transient notice (<c>08_UI</c> §Notifications policy, "in-app, transient"), behind an interface so view models are testable. Safety events never go here.</summary>
public interface IMessageStrip
{
    void Info(string title, string body);

    void Success(string title, string body);

    void Warn(string title, string body);
}

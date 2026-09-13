using System.Reflection;

namespace FrameLedger.App.Services;

/// <summary>What the App says about itself in <c>Hello</c> and in About.</summary>
internal static class UiIdentity
{
    public static string Version { get; } =
        typeof(UiIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UiIdentity).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}

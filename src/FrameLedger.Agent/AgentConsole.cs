namespace FrameLedger.Agent;

/// <summary>
/// The console verbs' two streams. Operator-facing English, like the capture host's: no <c>.resx</c>
/// exists yet, and these lines are read by the person who typed the verb, never by the App.
/// </summary>
internal static class AgentConsole
{
    public static void Line(string text) => Console.Out.WriteLine(text);

    public static void Problem(string text) => Console.Error.WriteLine(text);
}

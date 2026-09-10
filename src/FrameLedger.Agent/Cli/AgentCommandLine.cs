using System.Globalization;

namespace FrameLedger.Agent.Cli;

/// <summary>
/// The Agent's command line (P2 PR-F): <c>--serve</c>, or <c>--console &lt;verb&gt;</c>, or one of the flags
/// <c>12_BUILD</c> §Debugging lists that P2 does not implement. The surface is the place that stays closed
/// (§S27: a user-named pid on a binary carrying no consent record), so it is the place that gets a test.
/// </summary>
/// <remarks>
/// <b><c>--data-dir</c> exists only under <c>--console</c></b> (HANDOFF §P2 decision D6): an integration test
/// needs a scratch ledger, and CI must never touch the profile. <c>--serve</c> with it is an error rather than an
/// ignored option, because an ignored option is how the product directory becomes selectable by accident.
/// </remarks>
internal sealed record AgentCommandLine
{
    public static readonly string[] AcceptedOptions = ["--exe", "--seconds", "--args", "--last", "--data-dir"];

    /// <summary>The flags the design names and P2 does not build; each answers "not implemented in P2", exit 2.</summary>
    public static readonly string[] NotImplementedFlags = ["--diag", "--install-task", "--uninstall-task", "--register-vklayer", "--unregister-vklayer"];

    private const string _usage =
        "usage: FrameLedger.Agent --serve\n"
        + "       FrameLedger.Agent --console [--data-dir <dir>] consent list | consent grant --exe <path> | consent revoke --exe <path>\n"
        + "                                                     | capture --exe <path> [--seconds <n>] | launch --exe <path> [--args \"<string>\"] [--seconds <n>]\n"
        + "                                                     | recover | sessions [--last <n>] | db path | games add --exe <path>\n"
        + "                                                     | killswitch on | killswitch off | killswitch status";

    public AgentVerb Verb { get; init; }

    public string? ExePath { get; init; }

    public int Seconds { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public int Last { get; init; } = 10;

    public string? DataDirectory { get; init; }

    /// <summary>The flag behind <see cref="AgentVerb.NotImplemented"/>.</summary>
    public string? Flag { get; init; }

    public string? Error { get; init; }

    public static AgentCommandLine Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return new AgentCommandLine { Error = _usage };
        }

        if (NotImplementedFlags.Contains(args[0], StringComparer.Ordinal))
        {
            return new AgentCommandLine { Verb = AgentVerb.NotImplemented, Flag = args[0] };
        }

        if (string.Equals(args[0], "--serve", StringComparison.Ordinal))
        {
            return args.Length == 1
                ? new AgentCommandLine { Verb = AgentVerb.Serve }
                : new AgentCommandLine { Error = "--serve takes no options; --data-dir exists only under --console (the product directory is not selectable)" };
        }

        if (!string.Equals(args[0], "--console", StringComparison.Ordinal))
        {
            return new AgentCommandLine { Error = _usage };
        }

        return ParseConsole(args[1..]);
    }

    private static AgentCommandLine ParseConsole(string[] rest)
    {
        (AgentVerb verb, int at, int words) = VerbOf(rest);
        if (verb == AgentVerb.None)
        {
            return new AgentCommandLine { Error = _usage };
        }

        // Options may precede the verb (`--console --data-dir X capture ...`) or follow it: everything that is
        // not the verb's own words is read as options, in order.
        List<string> options = [];
        for (int i = 0; i < rest.Length; i++)
        {
            if (i < at || i >= at + words)
            {
                options.Add(rest[i]);
            }
        }

        return Options(verb, options);
    }

    private static AgentCommandLine Options(AgentVerb verb, List<string> options)
    {
        var parsed = new OptionState();
        for (int i = 0; i < options.Count; i++)
        {
            if (!options[i].StartsWith("--", StringComparison.Ordinal))
            {
                return new AgentCommandLine { Error = $"unexpected argument '{options[i]}'" };
            }

            if (!AcceptedOptions.Contains(options[i], StringComparer.Ordinal))
            {
                return new AgentCommandLine { Error = $"unknown option '{options[i]}'" };
            }

            if (i + 1 >= options.Count)
            {
                return new AgentCommandLine { Error = $"'{options[i]}' needs a value" };
            }

            if (parsed.Apply(options[i], options[++i]) is { } error)
            {
                return new AgentCommandLine { Error = error };
            }
        }

        if (verb is AgentVerb.ConsentGrant or AgentVerb.ConsentRevoke or AgentVerb.Capture or AgentVerb.Launch or AgentVerb.GamesAdd
            && string.IsNullOrWhiteSpace(parsed.Exe))
        {
            return new AgentCommandLine { Error = "--exe <path> is required" };
        }

        return new AgentCommandLine
        {
            Verb = verb,
            ExePath = parsed.Exe,
            Seconds = parsed.Seconds,
            Arguments = parsed.Arguments,
            Last = parsed.Last,
            DataDirectory = parsed.DataDirectory,
        };
    }

    /// <summary>The option values as they accumulate; <see cref="Apply"/> answers an error text or null.</summary>
    private sealed class OptionState
    {
        public string? Exe { get; private set; }

        public string? DataDirectory { get; private set; }

        public int Seconds { get; private set; }

        public int Last { get; private set; } = 10;

        public string Arguments { get; private set; } = string.Empty;

        public string? Apply(string option, string value)
        {
            switch (option)
            {
                case "--seconds":
                    // A non-numeric or non-positive value is an ERROR, never a silent 0: zero means "run until
                    // the target exits", and a mistyped bound must not become an unbounded session.
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0)
                    {
                        return "'--seconds' needs a positive whole number";
                    }

                    Seconds = seconds;
                    return null;
                case "--last":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int last) || last <= 0)
                    {
                        return "'--last' needs a positive whole number";
                    }

                    Last = last;
                    return null;
                case "--args":
                    Arguments = value;
                    return null;
                case "--data-dir":
                    DataDirectory = value;
                    return null;
                default:
                    Exe = value;
                    return null;
            }
        }
    }

    /// <summary>The verb, where it starts, and how many words it takes; the first run of non-option words.</summary>
    private static (AgentVerb Verb, int At, int Words) VerbOf(string[] rest)
    {
        int at = 0;
        while (at < rest.Length && rest[at].StartsWith("--", StringComparison.Ordinal))
        {
            at += 2;
        }

        if (at >= rest.Length)
        {
            return (AgentVerb.None, at, 0);
        }

        string first = rest[at];
        string? second = at + 1 < rest.Length && !rest[at + 1].StartsWith("--", StringComparison.Ordinal) ? rest[at + 1] : null;
        (AgentVerb verb, int words) = (first, second) switch
        {
            ("consent", "list") => (AgentVerb.ConsentList, 2),
            ("consent", "grant") => (AgentVerb.ConsentGrant, 2),
            ("consent", "revoke") => (AgentVerb.ConsentRevoke, 2),
            ("capture", _) => (AgentVerb.Capture, 1),
            ("launch", _) => (AgentVerb.Launch, 1),
            ("recover", _) => (AgentVerb.Recover, 1),
            ("sessions", _) => (AgentVerb.Sessions, 1),
            ("db", "path") => (AgentVerb.DbPath, 2),
            ("games", "add") => (AgentVerb.GamesAdd, 2),
            ("killswitch", "on") => (AgentVerb.KillSwitchOn, 2),
            ("killswitch", "off") => (AgentVerb.KillSwitchOff, 2),
            ("killswitch", "status") => (AgentVerb.KillSwitchStatus, 2),
            _ => (AgentVerb.None, 0),
        };

        return (verb, at, words);
    }
}

namespace FrameLedger.App.Services;

/// <summary>The Presented-FPS qualifier (<c>03_METRICS</c> §Presented FPS), as the row's <c>presented_qualifier</c> and the wire's spell it.</summary>
public enum FpsQualifier
{
    /// <summary><c>census_not_run</c>.</summary>
    CensusNotRun,

    /// <summary><c>no_fg_runtime</c>: muted chip.</summary>
    NoRuntime,

    /// <summary><c>fg_runtime_loaded</c>: warning chip.</summary>
    RuntimeLoaded,

    /// <summary><c>none_withheld</c>: the counted <c>none</c> was withheld (§H5).</summary>
    Withheld,
}

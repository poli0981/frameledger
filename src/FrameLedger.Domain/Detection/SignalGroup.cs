namespace FrameLedger.Domain.Detection;

/// <summary>
/// Exactly one of <c>any</c> / <c>all</c> over a flat list of signals.
/// </summary>
/// <remarks>
/// It holds signals, never groups. The schema forbids nesting in v2
/// (<c>maxProperties: 1</c> plus its own <c>$comment</c>), so that constraint is
/// expressed as a type here rather than as a validation rule someone has to
/// remember. This remark used to say it is why both RPG Maker rows could not be
/// expressed; since 2026-09-16 both are, without nesting (<c>05_DETECTION</c>
/// §Engine signatures), and FromSoftware's two archive halves are one <c>all</c>
/// group (2026-09-23).
/// </remarks>
public sealed record SignalGroup
{
    /// <summary>How the signals combine.</summary>
    public required SignalCombinator Combinator { get; init; }

    /// <summary>The signals. Never empty — the schema sets <c>minItems: 1</c>.</summary>
    public required IReadOnlyList<DetectionSignal> Signals { get; init; }
}

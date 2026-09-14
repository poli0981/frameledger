using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Services;

/// <summary>FR-8.3's answer: the override (null = use the measured value) and whether it is also the game's default.</summary>
public sealed record TriStateOverrideChoice(Tri? Override, bool SetAsGameDefault);

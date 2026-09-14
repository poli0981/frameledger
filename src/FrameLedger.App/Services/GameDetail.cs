using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>Everything the detail page shows for one game, loaded in one pass: the row, its sessions newest first, their annotations, the aggregate.</summary>
public sealed record GameDetail(
    GameRow Row,
    IReadOnlyList<SessionRow> Sessions,
    IReadOnlyDictionary<long, SessionAnnotation> Annotations,
    GameSessionSummary? Summary);

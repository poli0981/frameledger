using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>One library card's data: the row and its sessions in aggregate (null when it has none).</summary>
public sealed record GameCard(GameRow Row, GameSessionSummary? Summary);

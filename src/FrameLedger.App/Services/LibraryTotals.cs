namespace FrameLedger.App.Services;

/// <summary>The Dashboard's totals strip (<c>08_UI</c> §Dashboard): games tracked, total playtime, sessions in the last seven days.</summary>
public sealed record LibraryTotals(int GamesTracked, double TotalSeconds, long SessionsThisWeek);

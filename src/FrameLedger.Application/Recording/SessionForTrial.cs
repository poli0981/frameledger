namespace FrameLedger.Application.Recording;

/// <summary>
/// D38: the session the recorder just finished, as the exception's trial needs it — the game row it was stored under,
/// whether it was stored as a hooked session, and the frames it recorded.
/// </summary>
/// <param name="GameId">The <c>games</c> row the session belongs to.</param>
/// <param name="StoredHooked">It was hooked AND its row was written (a session too short to keep proves nothing).</param>
/// <param name="FrameCount">Frames the row records.</param>
public readonly record struct SessionForTrial(long GameId, bool StoredHooked, long FrameCount);

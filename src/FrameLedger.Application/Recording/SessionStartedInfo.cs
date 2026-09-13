using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What is known about a session the moment it starts — before any attach, before any record. <c>QpcFrequency</c>
/// is the records' clock rate, for a consumer computing rates over them.
/// </summary>
public sealed record SessionStartedInfo(
    Guid SessionGuid,
    long GameId,
    string GameName,
    string ExePath,
    CaptureMode Mode,
    DateTimeOffset StartedAt,
    long QpcFrequency);

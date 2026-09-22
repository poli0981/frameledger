namespace FrameLedger.Application.Persistence;

/// <summary>
/// The session's <c>games</c> row was deleted between the finalizer's owner lookup and its write (2026-09-23): the
/// repository checks inside the write transaction and throws this rather than letting the foreign key throw a
/// <c>SqliteException</c> the caller cannot tell from a real fault. The finalizer answers it as
/// <c>FinalizeStatus.GameRemoved</c>.
/// </summary>
public sealed class SessionOwnerMissingException : Exception
{
    public SessionOwnerMissingException()
    {
    }

    public SessionOwnerMissingException(string message)
        : base(message)
    {
    }

    public SessionOwnerMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

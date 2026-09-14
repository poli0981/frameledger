namespace FrameLedger.App.Update;

/// <summary>An update step that did not complete, classified for the dialog (<see cref="UpdateFailure"/>); the cause is the inner exception.</summary>
public sealed class UpdateException : Exception
{
    public UpdateException()
    {
    }

    public UpdateException(string message)
        : base(message)
    {
    }

    public UpdateException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public UpdateException(UpdateFailure failure, string message, Exception? innerException)
        : base(message, innerException) => Failure = failure;

    public UpdateFailure Failure { get; } = UpdateFailure.Unknown;
}

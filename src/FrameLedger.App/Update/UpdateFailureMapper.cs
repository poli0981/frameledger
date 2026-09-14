using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Velopack.Exceptions;

namespace FrameLedger.App.Update;

/// <summary>
/// <c>11_UPDATER</c> §Error mapping as a function of the exception Velopack's client threw: the HTTP status when
/// there is one (404 → <see cref="UpdateFailure.NotFound"/>, 403/429 → <see cref="UpdateFailure.RateLimited"/>,
/// 5xx → <see cref="UpdateFailure.Server"/>), the transport failures → <see cref="UpdateFailure.Offline"/>, a
/// checksum → <see cref="UpdateFailure.Corrupt"/>, the rest → <see cref="UpdateFailure.Unknown"/>. Walks the inner
/// chain, because the source wraps what its downloader threw.
/// </summary>
public static class UpdateFailureMapper
{
    public static UpdateFailure Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (Classify(e) is { } failure)
            {
                return failure;
            }
        }

        return UpdateFailure.Unknown;
    }

    private static UpdateFailure? Classify(Exception e) => e switch
    {
        ChecksumFailedException => UpdateFailure.Corrupt,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => UpdateFailure.NotFound,
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } => UpdateFailure.RateLimited,
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 => UpdateFailure.Server,
        HttpRequestException { StatusCode: null } => UpdateFailure.Offline,
        HttpRequestException => UpdateFailure.Unknown,
        TaskCanceledException or TimeoutException or SocketException or IOException => UpdateFailure.Offline,
        _ => null,
    };
}

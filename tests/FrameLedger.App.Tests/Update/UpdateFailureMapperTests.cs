using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using FluentAssertions;
using FrameLedger.App.Update;
using Velopack.Exceptions;

namespace FrameLedger.App.Tests.Update;

/// <summary><c>11_UPDATER</c> §Error mapping, row by row, from the exception the client threw — including one wrapped by a source.</summary>
public sealed class UpdateFailureMapperTests
{
    public static TheoryData<Exception, UpdateFailure> Rows => new()
    {
        { new HttpRequestException("gone", null, HttpStatusCode.NotFound), UpdateFailure.NotFound },
        { new HttpRequestException("quota", null, HttpStatusCode.Forbidden), UpdateFailure.RateLimited },
        { new HttpRequestException("quota", null, HttpStatusCode.TooManyRequests), UpdateFailure.RateLimited },
        { new HttpRequestException("down", null, HttpStatusCode.ServiceUnavailable), UpdateFailure.Server },
        { new HttpRequestException("down", null, HttpStatusCode.BadGateway), UpdateFailure.Server },
        { new HttpRequestException("no such host"), UpdateFailure.Offline },
        { new TaskCanceledException("timeout"), UpdateFailure.Offline },
        { new SocketException(10060), UpdateFailure.Offline },
        { new TimeoutException(), UpdateFailure.Offline },
        { new InvalidOperationException("wrapped by the source", new HttpRequestException("gone", null, HttpStatusCode.NotFound)), UpdateFailure.NotFound },
        { new HttpRequestException("odd", null, HttpStatusCode.Unauthorized), UpdateFailure.Unknown },
        { new InvalidOperationException("no idea"), UpdateFailure.Unknown },
        { new ChecksumFailedException("FrameLedger-0.2.0-full.nupkg", "hash mismatch"), UpdateFailure.Corrupt },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheExceptionMapsToItsRow(Exception exception, UpdateFailure expected) => UpdateFailureMapper.Map(exception).Should().Be(expected);

    [Fact]
    public void TheClientsExceptionCarriesTheRowAndTheCause()
    {
        var cause = new HttpRequestException("gone", null, HttpStatusCode.NotFound);
        var ex = new UpdateException(UpdateFailureMapper.Map(cause), cause.Message, cause);
        ex.Failure.Should().Be(UpdateFailure.NotFound);
        ex.InnerException.Should().BeSameAs(cause);
        new UpdateException("bare").Failure.Should().Be(UpdateFailure.Unknown, "the standard constructors classify nothing");
    }
}

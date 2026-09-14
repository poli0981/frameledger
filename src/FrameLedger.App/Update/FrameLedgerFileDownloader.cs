using System.Net.Http;
using FrameLedger.App.Services;
using Velopack.Sources;

namespace FrameLedger.App.Update;

/// <summary>
/// Velopack's downloader with this product's <c>User-Agent</c> (<c>11_UPDATER</c>: <c>FrameLedger/{version}</c>) in
/// place of its own <c>Velopack/x.y.z</c>, which is a read-only static there. Nothing else changes: the GitHub source
/// adds no <c>Authorization</c> header for an empty token (measured on 1.2.0), so the request carries the UA and
/// GitHub's own <c>Accept</c>, and nothing that identifies the user.
/// </summary>
public sealed class FrameLedgerFileDownloader : HttpClientFileDownloader
{
    public static string UserAgentValue => "FrameLedger/" + UiIdentity.Version;

    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
    {
        HttpClient client = base.CreateHttpClient(headers, timeout);
        client.DefaultRequestHeaders.UserAgent.Clear();
        _ = client.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgentValue);
        return client;
    }
}

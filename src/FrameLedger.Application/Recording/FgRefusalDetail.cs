using System.Text.Json;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>sessions.fg_refusal_detail</c> (schema 0005, 2026-09-16): the numbers behind a <c>fg_refusal</c> token, as JSON.
/// <c>fg_refusal</c> said <em>that</em> the count refused — <c>non_uniform</c>, <c>multiple_streams</c> — and the
/// Dashboard's tooltip could only repeat the kind in words; <see cref="FgRefusal"/> had computed the bucket, its ratio
/// and the whole window's all along, and the aggregator threw them away. This is that record, kept. A reason, never
/// a measurement: nothing reads it to decide a number.
/// </summary>
/// <param name="Kind">The same token <c>fg_refusal</c> carries (<c>Vocabulary.FgRefusal</c>).</param>
/// <param name="Subject"><c>factor</c> or <c>presents_per_batch</c>.</param>
/// <param name="Count">Samples, streams or records, per <paramref name="Kind"/>.</param>
/// <param name="BucketIndex">Zero-based bucket that departed (<c>non_uniform</c>).</param>
/// <param name="BucketCount">How many buckets the window was split into.</param>
/// <param name="BucketValue">That bucket's ratio; null when it had no tokens at all (infinity is not JSON).</param>
/// <param name="Overall">The whole window's ratio.</param>
public sealed record FgRefusalDetail(
    string Kind,
    string Subject,
    int Count,
    int BucketIndex,
    int BucketCount,
    double? BucketValue,
    double Overall)
{
    /// <summary>True when the departing bucket held no tokens — the alt-tab / loading-screen shape.</summary>
    public bool BucketEmpty => BucketValue is null;

    public static FgRefusalDetail From(FgRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new FgRefusalDetail(
            Vocabulary.FgRefusal(refusal.Kind) ?? "none",
            refusal.Subject == FgRefusalSubject.Factor ? "factor" : "presents_per_batch",
            refusal.Count,
            refusal.BucketIndex,
            refusal.BucketCount,
            double.IsFinite(refusal.BucketValue) ? refusal.BucketValue : null,
            double.IsFinite(refusal.Overall) ? refusal.Overall : 0);
    }

    /// <summary>The column's text for <paramref name="refusal"/>.</summary>
    public static string ToJson(FgRefusal refusal) => JsonSerializer.Serialize(From(refusal), RecordingJsonContext.Default.FgRefusalDetail);

    /// <summary>The column's text back, or null for a null/empty/unparsable column (a writer that predates it, or a hand edit).</summary>
    public static FgRefusalDetail? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.FgRefusalDetail);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

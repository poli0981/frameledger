// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Advanced;

/// <summary>
/// The one JSON form of what "Advanced metrics" stores and carries (owner decision D102): the facts of a PC, the facts
/// of a session, the directory of its series, and — in a <c>.partial</c> — all of it together.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writing is strict and reading is tolerant</b>, like every JSON column here: what is written is exactly what the
/// types hold; what is read back and does not fit — a ledger edited by hand, a value in two shapes, a word that is not
/// a token — is left out, never thrown, and never guessed at.
/// </para>
/// <para>
/// <b>Only what the catalog says is read is written</b> (<see cref="Storable"/>). The readers have no way to produce
/// an identifier (D104); this is the same rule held once more at the door of the ledger, where a mistake would last.
/// </para>
/// </remarks>
public static class AdvancedJson
{
    /// <summary>
    /// The facts a ledger may hold: those whose id is a catalog entry this build reads. A fact of an entry that is an
    /// identifier, the player's state, or simply unknown is not among them.
    /// </summary>
    public static IReadOnlyList<AdvancedFact> Storable(IReadOnlyList<AdvancedFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return [.. facts.Where(static f => AdvancedMetricCatalog.Find(f.Id) is { IsRead: true })];
    }

    public static string OfFacts(IReadOnlyList<AdvancedFact> facts)
        => JsonSerializer.Serialize(Documents(facts), AdvancedJsonContext.Default.ListAdvancedFactDocument);

    public static IReadOnlyList<AdvancedFact> ParseFacts(string? json)
        => Read(json, AdvancedJsonContext.Default.ListAdvancedFactDocument) is { } documents ? Facts(documents) : [];

    public static string OfSession(IReadOnlyList<AdvancedFact> facts, AdvancedSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return JsonSerializer.Serialize(new AdvancedSessionDocument { Facts = Documents(facts), Sources = sources }, AdvancedJsonContext.Default.AdvancedSessionDocument);
    }

    public static (IReadOnlyList<AdvancedFact> Facts, AdvancedSources Sources) ParseSession(string? json)
        => Read(json, AdvancedJsonContext.Default.AdvancedSessionDocument) is { } document
            ? (Facts(document.Facts), document.Sources ?? AdvancedSources.Unknown)
            : ([], AdvancedSources.Unknown);

    public static string OfSeries(IReadOnlyList<AdvancedSeriesInfo> series, int dropped)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfNegative(dropped);
        return JsonSerializer.Serialize(new AdvancedSeriesDocument { Series = SeriesDocuments(series), Dropped = dropped }, AdvancedJsonContext.Default.AdvancedSeriesDocument);
    }

    public static (IReadOnlyList<AdvancedSeriesInfo> Series, int Dropped) ParseSeries(string? json)
        => Read(json, AdvancedJsonContext.Default.AdvancedSeriesDocument) is { } document
            ? (Series(document.Series), Math.Max(0, document.Dropped))
            : ([], 0);

    public static string OfStats(IReadOnlyDictionary<string, SensorSeriesStats> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return JsonSerializer.Serialize(new Dictionary<string, SensorSeriesStats>(stats, StringComparer.Ordinal), AdvancedJsonContext.Default.DictionaryStringSensorSeriesStats);
    }

    public static IReadOnlyDictionary<string, SensorSeriesStats> ParseStats(string? json)
        => Read(json, AdvancedJsonContext.Default.DictionaryStringSensorSeriesStats) ?? new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);

    /// <summary>A session's whole advanced context, for the <c>.partial</c>.</summary>
    public static string OfContext(AdvancedSessionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.Serialize(
            new AdvancedContextDocument
            {
                Machine = Documents(data.MachineFacts),
                Session = Documents(data.SessionFacts),
                Series = SeriesDocuments(data.Series),
                Sources = data.Sources,
            },
            AdvancedJsonContext.Default.AdvancedContextDocument);
    }

    /// <summary>The context a <c>.partial</c> carried, or null when it carried none that reads.</summary>
    public static AdvancedSessionData? ParseContext(string? json)
        => Read(json, AdvancedJsonContext.Default.AdvancedContextDocument) is { } document
            ? new AdvancedSessionData(Facts(document.Machine), Facts(document.Session), Series(document.Series), document.Sources ?? AdvancedSources.Unknown)
            : null;

    private static T? Read<T>(string? json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, type);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<AdvancedFactDocument> Documents(IReadOnlyList<AdvancedFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return
        [
            .. facts.Select(static f => new AdvancedFactDocument
            {
                Id = f.Id,
                Instance = f.Instance,
                Number = f.Value.Number,
                Text = f.Value.Text,
                Tokens = f.Value.Tokens is { } tokens ? [.. tokens] : null,
                Rows = f.Value.Rows is { } rows ? [.. rows.Select(static r => new AdvancedRowDocument { Name = r.Name, Value = r.Value, Token = r.Token })] : null,
            }),
        ];
    }

    private static List<AdvancedFact> Facts(List<AdvancedFactDocument>? documents)
    {
        var facts = new List<AdvancedFact>(documents?.Count ?? 0);
        foreach (AdvancedFactDocument d in documents ?? [])
        {
            if (!string.IsNullOrWhiteSpace(d?.Id) && ValueOf(d) is { } value)
            {
                facts.Add(new AdvancedFact(d.Id, string.IsNullOrWhiteSpace(d.Instance) ? null : d.Instance, value));
            }
        }

        return facts;
    }

    /// <summary>The value of a stored fact when it has exactly one shape and that shape is well formed; null otherwise.</summary>
    private static AdvancedValue? ValueOf(AdvancedFactDocument d)
    {
        int shapes = (d.Number is null ? 0 : 1) + (d.Text is null ? 0 : 1) + (d.Tokens is null ? 0 : 1) + (d.Rows is null ? 0 : 1);
        if (shapes != 1)
        {
            return null;
        }

        if (d.Number is { } number)
        {
            return double.IsFinite(number) ? AdvancedValue.Of(number) : null;
        }

        if (d.Text is { } text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : AdvancedValue.OfText(text);
        }

        if (d.Tokens is { } tokens)
        {
            return tokens.TrueForAll(static t => AdvancedValue.IsToken(t)) ? AdvancedValue.OfTokens(tokens) : null;
        }

        // A row with no name or no value is not a row: the whole fact is left out rather than shown with a hole in it.
        var rows = new List<AdvancedRow>(d.Rows!.Count);
        foreach (AdvancedRowDocument? r in d.Rows)
        {
            if (r?.Name is not { } name || r.Value is not { } value)
            {
                return null;
            }

            rows.Add(new AdvancedRow(name, value, r.Token));
        }

        return AdvancedValue.OfRows(rows);
    }

    private static List<AdvancedSeriesInfoDocument> SeriesDocuments(IReadOnlyList<AdvancedSeriesInfo> series)
        => [.. series.Select(static s => new AdvancedSeriesInfoDocument { Series = s.Series, MetricId = s.MetricId, Label = s.Label })];

    /// <summary>The directory lines that are whole and name an advanced series; any other is left out.</summary>
    private static List<AdvancedSeriesInfo> Series(List<AdvancedSeriesInfoDocument>? documents)
    {
        var series = new List<AdvancedSeriesInfo>(documents?.Count ?? 0);
        foreach (AdvancedSeriesInfoDocument? d in documents ?? [])
        {
            if (d?.Series is { } name && d.MetricId is { } metric && d.Label is { } label && name.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal))
            {
                series.Add(new AdvancedSeriesInfo(name, metric, label));
            }
        }

        return series;
    }
}

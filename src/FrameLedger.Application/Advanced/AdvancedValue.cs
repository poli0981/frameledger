// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// The value of one advanced fact, in exactly one of four shapes. There is no shape for "unknown": a fact that was
/// not read is absent, and the page shows N/A with the reason (rule 7's "never a fabricated value", for every unit).
/// </summary>
/// <remarks>
/// WORDS THE VENDOR WROTE AND WORDS WE WROTE ARE KEPT APART. <see cref="Text"/> is the vendor's — a firmware version,
/// a chipset's name — and is shown as it is. <see cref="Tokens"/> are ours — <c>discrete</c>, <c>hdr10</c> — fixed
/// lower-case words the App translates, because every string a user reads comes from the resources and a stored
/// ledger must read the same in the three languages.
/// </remarks>
public sealed record AdvancedValue
{
    private AdvancedValue()
    {
    }

    /// <summary>A number in the entry's unit; a flag is 1 or 0.</summary>
    public double? Number { get; private init; }

    /// <summary>The vendor's own words.</summary>
    public string? Text { get; private init; }

    /// <summary>Our own fixed words, in the order they are shown. May be empty: the source answered, and named nothing.</summary>
    public IReadOnlyList<string>? Tokens { get; private init; }

    /// <summary>The rows of a table.</summary>
    public IReadOnlyList<AdvancedRow>? Rows { get; private init; }

    public static AdvancedValue Of(double number)
    {
        if (!double.IsFinite(number))
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "a fact's number is finite: one that was not read is absent, not NaN");
        }

        return new AdvancedValue { Number = number };
    }

    public static AdvancedValue Of(bool flag) => new() { Number = flag ? 1 : 0 };

    public static AdvancedValue OfText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new AdvancedValue { Text = text };
    }

    public static AdvancedValue OfTokens(params IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (string token in tokens)
        {
            if (!IsToken(token))
            {
                throw new ArgumentException($"'{token}' is not a token: lower-case letters, digits and underscores", nameof(tokens));
            }
        }

        return new AdvancedValue { Tokens = [.. tokens] };
    }

    public static AdvancedValue OfRows(IReadOnlyList<AdvancedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new AdvancedValue { Rows = [.. rows] };
    }

    /// <summary>True for a word made of <c>a-z</c>, <c>0-9</c> and <c>_</c> that starts with a letter.</summary>
    public static bool IsToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token[0] is not (>= 'a' and <= 'z'))
        {
            return false;
        }

        foreach (char c in token)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }
}

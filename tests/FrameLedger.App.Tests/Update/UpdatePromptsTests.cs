// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary>
/// beta.14: the offer says the download Velopack will make. With deltas in the feed (beta.13 on) that is a few megabytes,
/// and the offer said the full package's ~102 MB.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the expected texts are resources that follow the UI culture")]
public sealed class UpdatePromptsTests
{
    [Fact]
    public void WithADeltaTheOfferSaysItsSizeAndNamesTheFullPackageAsTheFallback()
    {
        var candidate = new UpdateCandidate("0.1.0-beta.14", null, 107_131_729) { DeltaBytes = 1_599_033 };

        UpdatePrompts.OfferText(candidate).Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Update_Available_Body_Delta_Format,
            "0.1.0-beta.14", 1.5.ToString("0.#", CultureInfo.CurrentCulture), "102"));
    }

    [Fact]
    public void WithoutADeltaTheOfferSaysTheFullPackage()
    {
        var candidate = new UpdateCandidate("0.1.0-beta.14", null, 107_131_729);

        UpdatePrompts.OfferText(candidate).Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Update_Available_Body_Format, "0.1.0-beta.14", "102"));
    }
}

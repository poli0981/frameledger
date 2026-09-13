using System.Globalization;
using FluentAssertions;
using SafetyStrings = FrameLedger.Shared.Strings;

namespace FrameLedger.App.Tests;

/// <summary>
/// The <c>Safety_*</c> family in <c>FrameLedger.Shared</c> as the App loads it: every key in en/vi/ja, vi really
/// translated, and ja the English verbatim until a reviewer signs (<c>09_I18N</c> §Safety-critical strings) —
/// the audit checks the files, this checks what the satellites actually resolve to.
/// </summary>
public sealed class SafetyStringsTests
{
    [Fact]
    public void EverySafetyKeyResolvesInEveryLanguageAndJaIsTheEnglishUntilReviewed()
    {
        SafetyStrings.Keys.Should().NotBeEmpty().And.OnlyContain(static k => k.StartsWith("Safety_", StringComparison.Ordinal), "Shared holds the reviewed-as-legal family and nothing else yet");
        var en = CultureInfo.GetCultureInfo("en");
        var vi = CultureInfo.GetCultureInfo("vi");
        var ja = CultureInfo.GetCultureInfo("ja");
        int translated = 0;
        foreach (string key in SafetyStrings.Keys)
        {
            string e = SafetyStrings.ResourceManager.GetString(key, en)!;
            string v = SafetyStrings.ResourceManager.GetString(key, vi)!;
            string j = SafetyStrings.ResourceManager.GetString(key, ja)!;
            e.Should().NotBeNullOrWhiteSpace(key);
            v.Should().NotBeNullOrWhiteSpace(key);
            j.Should().Be(e, $"{key}: ja is marked 'safety: human review required', so the English is what ships");
            if (!string.Equals(v, e, StringComparison.Ordinal))
            {
                translated++;
            }
        }

        translated.Should().Be(SafetyStrings.Keys.Count, "every vi value is a translation, not a copy");
    }

    [Fact]
    public void TheDialogRecommendsNeitherTierInAnyLanguage()
    {
        // 19_SAFETY §User-facing consent: "limited", "reduced", "degraded" never describe Tier 2, and no sentence
        // names what the user loses by declining. A word check is not a review, but it is the drift a review would miss.
        // ("recommend" is not on the list: the intro says, on purpose, that the dialog recommends neither choice.)
        string[] forbidden = ["limited", "reduced", "degraded", "you lose", "you will lose", "we suggest", "hạn chế", "suy giảm", "bạn sẽ mất", "nên bật", "nên chọn"];
        foreach (string culture in new[] { "en", "vi" })
        {
            var ci = CultureInfo.GetCultureInfo(culture);
            foreach (string key in SafetyStrings.Keys.Where(static k => k.StartsWith("Safety_Consent_", StringComparison.Ordinal)))
            {
                string text = SafetyStrings.ResourceManager.GetString(key, ci)!;
                foreach (string word in forbidden)
                {
                    text.Should().NotContainEquivalentOf(word, $"{key} ({culture})");
                }
            }
        }
    }
}

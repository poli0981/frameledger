// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;
using FluentAssertions;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.15: a list of choices built once, in a static, keeps the language the App started in after a change of language,
/// while the page around it — rebuilt — follows. The themes, the update channels, the Logs page's two lists and the
/// Games page's sort order were such lists until beta.15; this keeps a new one from appearing. The language list is the
/// one exception: each language is named in its own words, the same in every language.
/// </summary>
public sealed class LanguageFollowingTests
{
    [Fact]
    public void NoViewModelKeepsAStaticListOfLabelledChoices()
    {
        string[] statics =
        [
            .. typeof(SettingsViewModel).Assembly.GetTypes()
                .Where(static t => string.Equals(t.Namespace, "FrameLedger.App.ViewModels", StringComparison.Ordinal))
                .SelectMany(static t => t.GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => (Type: t, Property: p)))
                .Where(static x => x.Property.PropertyType.IsGenericType
                                   && x.Property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                                   && x.Property.PropertyType.GetGenericArguments()[0] is { IsGenericType: true } item
                                   && item.GetGenericTypeDefinition() == typeof(Choice<>))
                .Select(static x => x.Type.Name + "." + x.Property.Name),
        ];

        statics.Should().Equal(["SettingsViewModel.Languages"], "every other list is labelled in a language, and must be built per page");
    }
}

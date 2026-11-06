// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Pages;
using Wpf.Ui.Abstractions;

namespace FrameLedger.App.Services;

/// <summary>
/// The window's page provider (beta.34, owner request 2026-11-06: "switching to Games stalls 0.5–1.5 s"): the container's,
/// except that the library page is built once per window and shown again on every later visit — its cards already laid
/// out — and reads the library again in the background (<see cref="ViewModels.GamesViewModel.Attach"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not WPF UI's own cache.</b> <c>NavigationViewItem.NavigationCacheMode</c> is read only when the NavigationView has
/// neither a service provider nor a page provider (upstream 4.3.0 <c>NavigationView.Navigation.cs</c>,
/// <c>GetNavigationItemInstance</c>): this App sets a page provider, so the attribute would have changed nothing.
/// </para>
/// <para>
/// <b>Why per window.</b> A language change builds a new window (<see cref="ShellHost.Rebuild"/>) and shows it before the
/// old one closes: a page shared between them would be asked into a second frame while the first still holds it, and would
/// keep the old language's text. One instance of this class per <see cref="MainWindow"/> keeps both apart — the new window
/// builds its own page in the new language.
/// </para>
/// <para>
/// The pages stay transient in the container (<c>16_WPFUI_SYNTAX</c> §Navigation); only <see cref="Kept"/> are held, and
/// only by the window that built them.
/// </para>
/// </remarks>
public sealed class WindowPageCache(INavigationViewPageProvider inner) : INavigationViewPageProvider
{
    private readonly INavigationViewPageProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly Dictionary<Type, object> _kept = [];

    /// <summary>The pages a window keeps: the library, the one page whose build grows with the data (<c>spike-notes</c> §21, §27).</summary>
    public static IReadOnlySet<Type> Kept { get; } = new HashSet<Type> { typeof(GamesPage) };

    public object? GetPage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (!Kept.Contains(pageType))
        {
            return _inner.GetPage(pageType);
        }

        if (_kept.TryGetValue(pageType, out object? page))
        {
            return page;
        }

        page = _inner.GetPage(pageType);
        if (page is not null)
        {
            _kept[pageType] = page;
        }

        return page;
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The two navigation moves a view model makes, behind an interface so a test can watch them without a <c>NavigationView</c>.</summary>
public interface IPageNavigator
{
    void Navigate<TPage>() where TPage : class;

    void GoBack();
}

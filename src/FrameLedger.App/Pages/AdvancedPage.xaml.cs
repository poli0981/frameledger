// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

/// <summary>
/// The Advanced page (owner decision D101; <c>08_UI</c> §Advanced). The view model asks the Agent about this PC only
/// between <c>Loaded</c> and <c>Unloaded</c> — which is what lets the Agent stop reading sensors when no game runs
/// and the page is closed (D107) — and the two charts are drawn here from the lines it presents.
/// </summary>
public partial class AdvancedPage : INavigableView<AdvancedViewModel>
{
    public AdvancedPage(AdvancedViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();

        ViewModel.LivePresented += OnLivePresented;
        ViewModel.SessionPresented += OnSessionPresented;
        OnLivePresented(this, EventArgs.Empty);
        OnSessionPresented(this, EventArgs.Empty);
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
        IsVisibleChanged += (_, e) =>
        {
            // A window hidden to the tray is not on screen either: the Agent is not asked for a page nobody sees.
            if (e.NewValue is true && IsLoaded)
            {
                ViewModel.Attach();
            }
            else if (e.NewValue is false)
            {
                ViewModel.Detach();
            }
        };
    }

    public AdvancedViewModel ViewModel { get; }

    private void OnLivePresented(object? sender, EventArgs e) => LiveChart.Show(ViewModel.LiveLines, ViewModel.LiveUnit);

    private void OnSessionPresented(object? sender, EventArgs e) => SessionChart.Show(ViewModel.SessionLines, ViewModel.SessionUnit);
}

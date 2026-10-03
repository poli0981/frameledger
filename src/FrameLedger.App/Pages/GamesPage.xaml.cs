// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Input;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class GamesPage : INavigableView<GamesViewModel>
{
    public GamesPage(GamesViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
        // beta.11: the page reloads itself while it is on screen (a session ends, an import adds games), and lets go after.
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public GamesViewModel ViewModel { get; }

    /// <summary>FR-1.1's drop: files only (a shortcut's target needs the shell link reader, later).</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _ = ViewModel.DropAsync(paths);
        }
    }

    /// <summary>The list view opens a game on a double-click, as the cards do on a click.</summary>
    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    /// <summary>…and on Enter, so the list is usable from the keyboard.</summary>
    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OpenSelected()
    {
        if (GameList.SelectedItem is GameCardViewModel card)
        {
            ViewModel.OpenCommand.Execute(card);
        }
    }
}

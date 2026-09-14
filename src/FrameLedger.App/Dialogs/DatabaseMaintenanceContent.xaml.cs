using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of Tools ▸ Database maintenance's <c>ContentDialog</c> (P4 PR-7).</summary>
public partial class DatabaseMaintenanceContent : UserControl
{
    public DatabaseMaintenanceContent(DatabaseMaintenanceViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public DatabaseMaintenanceViewModel ViewModel { get; }
}

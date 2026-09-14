using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Controls;

/// <summary>
/// <c>08_UI</c> §Tri-state feature chips: Yes = filled accent, No = outlined, N/A = dashed outline with muted text;
/// the source (measured / manual / inherited) is the tooltip. The override flyout (FR-8.3) is PR-6's.
/// </summary>
public sealed class TriStateChip : Control
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(TriStateChipModel), typeof(TriStateChip), new PropertyMetadata(null));

    static TriStateChip() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TriStateChip), new FrameworkPropertyMetadata(typeof(TriStateChip)));

    public TriStateChipModel? Model
    {
        get => (TriStateChipModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }
}

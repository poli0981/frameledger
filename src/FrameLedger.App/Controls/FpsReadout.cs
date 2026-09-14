using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.Services;

namespace FrameLedger.App.Controls;

/// <summary>
/// <c>08_UI</c> §FPS display rule as a control (<c>16_WPFUI_SYNTAX</c> §Custom controls): one <see cref="Model"/>
/// in, the three shapes out — <c>62 → 118 FPS (×1.9 FG)</c> with the factor as a chip; <c>144 FPS</c> alone; or
/// Presented FPS with its census qualifier chip (muted, or a warning). The template is in
/// <c>Styles/FrameLedger.xaml</c> and uses theme brushes only.
/// </summary>
public sealed class FpsReadout : Control
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(FpsReadoutModel), typeof(FpsReadout), new PropertyMetadata(FpsReadoutModel.Unavailable));

    static FpsReadout() => DefaultStyleKeyProperty.OverrideMetadata(typeof(FpsReadout), new FrameworkPropertyMetadata(typeof(FpsReadout)));

    public FpsReadoutModel Model
    {
        get => (FpsReadoutModel)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }
}

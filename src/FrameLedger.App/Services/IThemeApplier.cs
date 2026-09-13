using System.Windows;

namespace FrameLedger.App.Services;

/// <summary>
/// The WPF UI theme calls behind a seam, so a view model is testable without an <c>Application</c>
/// (<c>ApplicationThemeManager</c> touches <c>Application.Current.Resources</c>).
/// </summary>
public interface IThemeApplier
{
    /// <summary>Apply <paramref name="theme"/>; <paramref name="window"/> is watched for system changes when the choice is System, and unwatched otherwise.</summary>
    void Apply(AppTheme theme, Window? window);
}

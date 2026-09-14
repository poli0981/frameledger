using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>16_WPFUI_SYNTAX</c> §Theme rules: manual Light/Dark through <c>ApplicationThemeManager.Apply</c>; System
/// through <c>SystemThemeWatcher.Watch</c> on the window — which needs an HWND, so the caller passes the window
/// from its <c>Loaded</c> handler and never from a constructor — and <c>UnWatch</c> when switching to manual.
/// </summary>
public sealed class WpfThemeApplier : IThemeApplier
{
    public void Apply(AppTheme theme, Window? window)
    {
        switch (theme)
        {
            case AppTheme.System:
                ApplicationThemeManager.Apply(SystemIsDark() ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.Mica, true);
                if (window is { IsLoaded: true })
                {
                    SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, true);
                }

                break;
            case AppTheme.Light:
            case AppTheme.Dark:
                if (window is not null)
                {
                    SystemThemeWatcher.UnWatch(window);
                }

                ApplicationThemeManager.Apply(theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.Mica, true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(theme), theme, "not a theme");
        }
    }

    private static bool SystemIsDark() => ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.HCBlack or SystemTheme.Glow or SystemTheme.CapturedMotion;
}

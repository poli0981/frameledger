# 16 — WPF UI (Wpf.Ui) syntax rules

Mandatory conventions for all XAML/C# in `FrameLedger.App`. WPF UI = lepoco's Fluent library (<https://wpfui.lepo.co/>). **Read this before writing any view.**

## Packages & pinning

| Package (NuGet id) | Pin | Notes |
|---|---|---|
| `WPF-UI` | **= 4.3.0** (exact; bump deliberately) | Assembly/namespace is `Wpf.Ui`. Targets `net8.0-windows` → resolves cleanly for our `net10.0-windows10.0.19041.0` TFM (`12_BUILD` §Managed build) |
| `WPF-UI.Abstractions` | transitive | Navigation abstractions (`INavigationViewPageProvider`) |
| `WPF-UI.DependencyInjection` | match `WPF-UI` | DI glue (`services.AddNavigationViewPageProvider()`); verify exact id/version at scaffold |
| `WPF-UI.Tray` | **not used** | Tray stays on H.NotifyIcon.Wpf (CLAUDE.md stack table) |

> ⚠ **Version hygiene:** the 4.x line has multiple NuGet-deprecated releases with critical bugs (4.0.0–4.0.3, 4.1.0, 4.2.0). Never let the version float, never downgrade below 4.2.1, and read release notes on every bump. Record the pin in `Directory.Packages.props` with a dated comment.

## Namespace

One URI for everything — controls, markup extensions, dictionaries:

```xml
xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
```

## App bootstrap (Generic Host + theme)

`App.xaml` — dictionaries **in this order** (Themes before Controls):

```xml
<Application x:Class="FrameLedger.App.App" ...
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ui:ThemesDictionary Theme="Dark" />
        <ui:ControlsDictionary />
        <ResourceDictionary Source="Styles/FrameLedger.xaml" /> <!-- our styles LAST -->
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

`App.xaml.cs` — Generic Host owns everything:

```csharp
private static readonly IHost _host = Host.CreateDefaultBuilder()
    .ConfigureServices((ctx, services) =>
    {
        services.AddNavigationViewPageProvider();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        // pages + their viewmodels: Transient
        services.AddTransient<DashboardPage>(); services.AddTransient<DashboardViewModel>();
        // ... Application-layer ports, IpcClient, repositories, etc.
    })
    .Build();
```

**As built (P3 PR-2, 2026-09-13), three deviations from the sketch above:** `Host.CreateApplicationBuilder()` rather than `CreateDefaultBuilder`; `MainWindow` is **transient** (a language change rebuilds it in the new culture — `09_I18N` §Mechanics — and `Services.ShellHost` tracks the live one), so `App.xaml` sets `ShutdownMode="OnExplicitShutdown"` and the host's `IHostApplicationLifetime` is what ends the process (the shell host stops it when the live window closes; the old window of a rebuild closing is not an exit); and the run is one `Task` started from a synchronous `OnStartup` (`async void` overrides are VSTHRD100 under this repo's analyzers) that opens the ledger, starts the host, awaits its shutdown, tears down, and only then calls `Shutdown()`.

Theme rules:
- Manual Light/Dark: `ApplicationThemeManager.Apply(ApplicationTheme.Dark)`.
- System: `SystemThemeWatcher.Watch(mainWindow)` — call it in the window's `Loaded` handler (needs an HWND), and unwatch when switching to manual.
- Persist the choice in `settings`; re-apply before the main window shows to avoid a flash. **Measured 2026-09-13: this is a correctness rule, not a cosmetic one** — `ApplicationThemeManager.Apply(theme, backdrop, updateAccent: true)` is what registers the `SystemAccentColor*` resources the control templates `StaticResource` at first Measure; a `FluentWindow` shown before any `Apply` throws `XamlParseException: Cannot find resource named 'SystemAccentColorPrimary'`. The shell applies the theme in its constructor before `InitializeComponent()` and again from `Loaded` for the watcher.
- Subscribe `ApplicationThemeManager.Changed` once, centrally, to re-theme ScottPlot (below).

## Main window skeleton

```xml
<ui:FluentWindow x:Class="FrameLedger.App.MainWindow" ...
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    ExtendsContentIntoTitleBar="True"
    WindowBackdropType="Mica"
    WindowCornerPreference="Round"
    WindowStartupLocation="CenterScreen">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/> <!-- TitleBar -->
      <RowDefinition Height="Auto"/> <!-- Menu -->
      <RowDefinition Height="*"/>    <!-- NavigationView -->
    </Grid.RowDefinitions>

    <ui:TitleBar Grid.Row="0" Title="FrameLedger" Icon="pack://application:,,,/Assets/icon.ico" />

    <Menu Grid.Row="1"> <!-- classic menu, Fluent-restyled automatically --> </Menu>

    <ui:NavigationView x:Name="RootNavigation" Grid.Row="2"
                       PaneDisplayMode="LeftFluent"
                       IsBackButtonVisible="Collapsed"
                       IsPaneToggleVisible="False">
      <ui:NavigationView.MenuItems>
        <ui:NavigationViewItem Content="{x:Static res:Strings.Nav_Dashboard}"
                               Icon="{ui:SymbolIcon Home24}"
                               TargetPageType="{x:Type pages:DashboardPage}" />
        <!-- Games, Compare, Logs -->
      </ui:NavigationView.MenuItems>
      <ui:NavigationView.FooterMenuItems>
        <ui:NavigationViewItem Content="{x:Static res:Strings.Nav_Settings}"
                               Icon="{ui:SymbolIcon Settings24}"
                               TargetPageType="{x:Type pages:SettingsPage}" />
      </ui:NavigationView.FooterMenuItems>
    </ui:NavigationView>

    <ui:SnackbarPresenter x:Name="SnackbarPresenter" Grid.Row="2" VerticalAlignment="Bottom" />
    <ContentPresenter x:Name="DialogHost" Grid.Row="2" /> <!-- ContentDialogService host -->
  </Grid>
</ui:FluentWindow>
```

Code-behind wires services once: `navigationService.SetNavigationControl(RootNavigation)`, `snackbarService.SetSnackbarPresenter(SnackbarPresenter)`, `contentDialogService.SetDialogHost(DialogHost)` (member names per the pinned version's API — confirm at scaffold, they occasionally shift between majors).

## Navigation

- Navigate **only** via `INavigationService.Navigate(typeof(SomePage))` — never manipulate `Frame` directly, never `new` a Page.
- Pages implement `INavigableView<TViewModel>` with `ViewModel` injected via constructor DI; `DataContext = this` in the page constructor (WPF UI template idiom) so bindings read `ViewModel.*`.
- Page state: pages are Transient; anything that must survive navigation lives in the ViewModel-backing services/DB, not the Page.

## Control mapping (always prefer these)

| Need | Use | Not |
|---|---|---|
| Button | `ui:Button` with `Appearance="Primary/Secondary/Danger"`, `Icon="{ui:SymbolIcon Play24}"` | bare `Button` |
| Text | `ui:TextBlock` with `FontTypography="Caption/Body/BodyStrong/Subtitle/Title"` | hardcoded FontSize |
| Text input | `ui:TextBox` (`PlaceholderText`, `Icon`) | native TextBox |
| Numbers | `ui:NumberBox` (`Minimum/Maximum/SmallChange`, `SpinButtonPlacementMode="Compact"` — needs ≥ 4.3.0) | TextBox + parsing |
| Toggle | `ui:ToggleSwitch` | CheckBox for on/off settings |
| Search | `ui:AutoSuggestBox` | |
| Cards / settings rows | `ui:Card`, `ui:CardControl`, `ui:CardExpander`, `ui:CardAction` | GroupBox |
| Banner | `ui:InfoBar` (`Severity`, `IsClosable`) | custom colored borders |
| Badge | `ui:InfoBadge` / `ui:Badge` | |
| Busy | `ui:ProgressRing` | |
| Contextual popup | `ui:Flyout` | Popup hand-rolling |
| Hyperlink-ish | `ui:HyperlinkButton`, `ui:Anchor` | |
| Grid of records | native `DataGrid` (WPF UI restyles it) | third-party grids |

Native `Menu`, `TabControl`, `ComboBox`, `Slider`, `ListView` are fine — the `ControlsDictionary` restyles standard WPF controls to Fluent automatically.

## Icons

- `{ui:SymbolIcon Symbol=Home24}` / `Icon="{ui:SymbolIcon Home24}"`; filled variant: `Filled="True"`. Symbols come from the bundled **Fluent System Icons** font — validate names against the `SymbolRegular` enum (IntelliSense), don't guess.
- **Never** use Segoe Fluent Icons glyphs/`FontIcon` with Segoe: that font is not bundled (license) and is absent on Windows 10 → empty squares. Fluent System Icons only.

## Theming & brushes

- Colors **only** via `{DynamicResource …}` theme keys, e.g. `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`, `ControlFillColorDefaultBrush`, `CardBackgroundFillColorDefaultBrush`, `ApplicationBackgroundBrush`, `AccentTextFillColorPrimaryBrush`, `SystemFillColorCriticalBrush`. `DynamicResource` always (theme can change at runtime); `StaticResource` for theme brushes is a review-blocking bug.
- Do **not** set `Background` on `FluentWindow` (kills Mica). Page backgrounds transparent by default.
- Exception to the no-hex rule: the chart palette — defined once per theme in `Styles/ChartPalette.xaml` (two dictionaries), never inline.

## Custom controls (`FpsReadout`, `TriStateChip`, sparklines)

- Derive from `Control` with `ControlTemplate` in `Styles/FrameLedger.xaml`; template uses only theme brushes above → they re-theme for free.
- `TriStateChip`: `Border` CornerRadius 12, states — Yes: `AccentFillColorDefaultBrush` bg + on-accent text; No: transparent bg + `ControlStrokeColorDefaultBrush` 1px border; N/A: dashed `StrokeDashArray="2 2"` border + `TextFillColorTertiaryBrush` text.
- **Built 2026-09-14 (P3 PR-5):** both derive from `Control` with one dependency property, `Model` (`FpsReadoutModel` / `TriStateChipModel`, decided by `Services.FpsPresentation` and the view models — the control renders, it never decides), templates in `Styles/FrameLedger.xaml` with `DataTrigger`s on the model's flags; the dashed N/A outline is a `Rectangle` behind the `Border` (a `Border` cannot dash). The source suffix icon became the tooltip: the Fluent System Icons names for 🔍/✎/↧ are not certain in this pinned version and a wrong `SymbolRegular` is a build error. The override flyout is PR-6's.
- No `SystemColors.*` anywhere.

## Dialogs & notifications

- Confirmations: `Wpf.Ui.Controls.MessageBox` — **async**, instance-based:

```csharp
var box = new Wpf.Ui.Controls.MessageBox
{
    Title = Strings.DeleteGame_Title,
    Content = Strings.DeleteGame_Body,
    PrimaryButtonText = Strings.Common_Delete,
    CloseButtonText = Strings.Common_Cancel,
};
var result = await box.ShowDialogAsync(); // Wpf.Ui.Controls.MessageBoxResult.Primary
```

  ⚠ Name clash with `System.Windows.MessageBox` — in App code, `using MessageBox = Wpf.Ui.Controls.MessageBox;` and ban the System one (BannedSymbols/analyzer note).
- Rich in-flow dialogs: `IContentDialogService` (host wired in MainWindow — a `ui:ContentDialogHost`, since 4.3.0 marks `SetDialogHost(ContentPresenter)` obsolete).
- Transient in-app: `ISnackbarService.Show(title, message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(4))`.
- System/tray notifications: H.NotifyIcon only (08_UI §Notifications policy).

## ScottPlot theme sync

On startup and on `ApplicationThemeManager.Changed`: for every live plot set figure/data background, axis/grid/tick colors, and series palette from the current theme resources (`ChartPalette.xaml`), then `Refresh()`. Centralize in `ChartTheme.Apply(Plot plot)` — pages never color plots ad hoc.

> **Built 2026-09-14 (P3 PR-6).** `Charts/ChartTheme.Apply(Plot)` + `ChartTheme.Attach(WpfPlot)` (weakly held; one
> subscription to `ApplicationThemeManager.Changed` re-applies and refreshes every live plot). The palette is
> **two** dictionaries, `Styles/ChartPalette.Dark.xaml` and `ChartPalette.Light.xaml`, `Color` resources with
> explicit ARGB hex — the one place hex is allowed — read into `Charts/ChartPalette` per theme and cached;
> `ChartPaletteTests` fails when the two files' key sets drift from what the record reads. Series colours come
> from the palette too (native, displayed, stutter, PSO stutter, sensors, segment span, histogram, percentile).
> Pinned `ScottPlot.WPF` 5.1.59: `Add.SignalXY` for the decimated series, `Add.Scatter` (no line) for markers,
> `Add.HorizontalSpan` for the ribbon, `Add.Bars` over `Statistics.Histogram.WithBinCount`, `Add.ScatterLine`
> for the percentile curve, a series' `Axes.YAxis = plot.Axes.Right` for the sensor overlay.

## Gotchas checklist

- [ ] Dictionaries order: `ThemesDictionary` → `ControlsDictionary` → app styles. Wrong order = default-looking controls.
- [ ] `SystemThemeWatcher.Watch` after HWND exists (`Loaded`), not in the constructor — but `ApplicationThemeManager.Apply(…, updateAccent: true)` BEFORE the first window's XAML loads, or the accent resources are missing at first Measure (§App bootstrap).
- [ ] Menu row lives **outside** `ui:TitleBar` (its area is the drag region; a Menu inside becomes undraggable/unclickable territory).
- [ ] Don't set `AllowsTransparency`/`WindowStyle` on `FluentWindow` — it manages its own chrome.
- [ ] Mica is Win 11-only: leave `WindowBackdropType="Mica"`; the library falls back on Win 10 — verify visuals in the Win 10 VM pass (14_TESTING matrix).
- [ ] VS Designer sometimes renders WPF UI controls unstyled at design time — judge by running, not the previewer.
- [ ] `TargetPageType` navigation requires the page registered in DI; a missing registration throws at runtime — smoke-test every nav item.
- [ ] resx localization unchanged: `Content="{x:Static res:Strings.Key}"` works on all WPF UI controls (09_I18N).
- [x] MIT license copy for WPF UI ships in `legal/licenses/` (THIRD_PARTY_NOTICES checklist) — ~~`wpfui-MIT.txt`, since 2026-09-14~~ `legal/licenses/nuget/WPF-UI.txt` since 2026-09-15, generated from the package's own LICENSE.md and carried into the published `licenses/`.
- [ ] **`ui:InfoBar` renders no Content in 4.3.0.** It is a `ContentControl`, the docs and the §Control mapping row
  above both say "banner with an action button", and the library template has no `ContentPresenter` — so a
  `ui:Button` written as the InfoBar's child compiles, binds, and is never drawn. With `IsClosable="False"` that is a
  bar with no exit, which is exactly how the safety notices shipped (2026-09-14). `Styles/FrameLedger.xaml` carries
  the upstream template plus one presenter column; `PagesLoadTests.TheInfoBarTemplateRendersItsContent` renders both
  templates and turns red when upstream gains a presenter, which is when the copy comes out.
- [ ] **No dialog in 4.3.0 closes on Esc.** Neither `ContentDialog.cs` nor `MessageBox.cs` at the 4.3.0 tag handles a
  key (read 2026-09-15), and `ContentDialog.CloseButtonText` DEFAULTS to "Close" — an empty string is how a dialog
  says it has no Close button. `MessageBox.Close()` is `[Obsolete("Use Close with MessageBoxResult instead")]` while no
  such overload is public; the Close button's own path is `TemplateButtonCommand.Execute(MessageBoxButton.Close)`.
  `Services/DialogKeyboard` registers one class handler on `Window` (bubbling `KeyDown`, so a dropdown's Esc wins) and
  closes through those paths; `AccessibilityTests.EscapeClosesTheOpenDialogWithNoResult` holds it (`08_UI` §Accessibility).

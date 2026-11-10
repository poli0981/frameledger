# 16 — WPF UI (Wpf.Ui) syntax rules

Mandatory conventions for all XAML/C# in `FrameLedger.App`. WPF UI = lepoco's Fluent library (<https://wpfui.lepo.co/>). **Read this before writing any view.**

## Packages & pinning

| Package (NuGet id) | Pin | Notes |
|---|---|---|
| `WPF-UI` | **= 4.3.0** (exact; bump deliberately) | Assembly/namespace is `Wpf.Ui`. Targets `net8.0-windows` → resolves cleanly for our `net11.0-windows10.0.22621.0` TFM — `net10.0-…` until beta.36 — (`12_BUILD` §Managed build; this row said `19041` until 2026-09-21, which is no TFM this repository ever built — `Directory.Build.props` has been `22621` with a 19045 floor throughout) |
| `WPF-UI.Abstractions` | ~~transitive~~ **= 4.3.0**, referenced directly (corrected 2026-10-04) | Navigation abstractions (`INavigationViewPageProvider`) |
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
- A Windows contrast theme (beta.35, D91): wins over all three while it is on, and is never left to the watcher, which would apply WPF UI's own high-contrast files (§Gotchas). `Services/ThemePlan.For` is the table, `Services/WpfThemeApplier` applies it.
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

    <ui:TitleBar Grid.Row="0" Title="FrameLedger" /> <!-- no Icon: no icon file exists yet (corrected 2026-10-04) -->

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
    <ui:ContentDialogHost x:Name="DialogHost" Grid.Row="2" /> <!-- §Dialogs; a ContentPresenter stood here until 2026-10-04 -->
  </Grid>
</ui:FluentWindow>
```

Code-behind wires services once: `navigationService.SetNavigationControl(RootNavigation)`, `snackbarService.SetSnackbarPresenter(SnackbarPresenter)`, `contentDialogService.SetDialogHost(DialogHost)` (member names per the pinned version's API — confirm at scaffold, they occasionally shift between majors).

## Navigation

- Navigate **only** via `INavigationService.Navigate(typeof(SomePage))` — never manipulate `Frame` directly, never `new` a Page.
- Pages implement `INavigableView<TViewModel>` with `ViewModel` injected via constructor DI; `DataContext = this` in the page constructor (WPF UI template idiom) so bindings read `ViewModel.*`.
- Page state: pages are Transient; anything that must survive navigation lives in the ViewModel-backing services/DB, not the Page.
  **Except the library, since beta.34** (owner request 2026-11-06, "Games stalls 0.5–1.5 s"; `spike-notes` §27): the window
  keeps the `GamesPage` it built — `Services.WindowPageCache`, the window's own `INavigationViewPageProvider`, set after
  `INavigationService.SetNavigationControl` (which hands the NavigationView the container's provider) — and its view model
  reads the library again each time the page is shown (`GamesViewModel.Attach`). Still Transient in the container; one
  instance per window, so a language change's new window builds its own in the new language. **Not**
  `NavigationViewItem.NavigationCacheMode`: WPF UI 4.3.0 reads it only when the NavigationView has neither a service
  provider nor a page provider (`NavigationView.Navigation.cs`, `GetNavigationItemInstance`), and this App sets one — the
  attribute would have changed nothing.

## Control mapping (always prefer these)

| Need | Use | Not |
|---|---|---|
| Button | `ui:Button` with `Appearance="Primary/Secondary/Danger"`, `Icon="{ui:SymbolIcon Play24}"` | bare `Button` |
| Text | `ui:TextBlock` with `FontTypography="Caption/Body/BodyStrong/Subtitle/Title"`; muted text with `Style="{StaticResource SecondaryText}"` or `TertiaryText` (beta.17) | hardcoded FontSize; `Appearance="Secondary"` on a TextBlock (§Gotchas) |
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
| Grid of records | native `DataGrid` (WPF UI restyles it — through the grid's own `RowStyle`/`CellStyle`/`ColumnHeaderStyle` setters, so a local one is based on WPF UI's keyed style: §Gotchas, added 2026-10-05) | third-party grids |

Native `Menu`, `TabControl`, `ComboBox`, `Slider`~~, `ListView`~~ are fine — the `ControlsDictionary` restyles standard WPF controls to Fluent automatically. *(Corrected 2026-10-05: 4.3.0 has no implicit style for the native `ListView`, `ListViewItem` or `GridViewColumnHeader` — only `ui:ListView` is restyled — nor for `DataGridRow`, `DataGridCell`, `DataGridColumnHeader` or `FlowDocumentScrollViewer`; read from the loaded `ControlsDictionary`.)*

## Icons

- `{ui:SymbolIcon Symbol=Home24}` / `Icon="{ui:SymbolIcon Home24}"`; filled variant: `Filled="True"`. Symbols come from the bundled **Fluent System Icons** font — validate names against the `SymbolRegular` enum (IntelliSense), don't guess.
- **Never** use Segoe Fluent Icons glyphs/`FontIcon` with Segoe: that font is not bundled (license) and is absent on Windows 10 → empty squares. Fluent System Icons only.

## Theming & brushes

- Colors **only** via `{DynamicResource …}` theme keys, e.g. `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`, `ControlFillColorDefaultBrush`, `CardBackgroundFillColorDefaultBrush`, `ApplicationBackgroundBrush`, `AccentTextFillColorPrimaryBrush`, `SystemFillColorCriticalBrush`. `DynamicResource` always (theme can change at runtime); `StaticResource` for theme brushes is a review-blocking bug. *(Added 2026-10-07: so is a property that looks like a theme colour but is looked up once — `ui:TextBlock Appearance`, §Gotchas.)*
- **`ui:Badge` `Appearance="Info"` / `Caution` / `Danger` / `Success` are NOT theme colours.** Their backgrounds are
  `Palette*Brush` from WPF UI's `Palette.xaml` — one fixed Material palette shared by both themes — under the theme's
  text: Info is white on `#03A9F4` in dark (2.6:1), measured 2026-09-16 on the Games card. ~~Use `Secondary` (theme
  fill)~~ **`Secondary` is refused too since 2026-09-21**: its trigger repaints the background only and keeps the default
  badge's on-accent `BadgeForeground`, near-white on near-white in the light theme. Use the `TagPill` + `TagPillText`
  styles (`Styles/FrameLedger.xaml`), or the `Pill` style with a `SystemFillColor*BackgroundBrush` and an explicit text
  brush; `ContrastTests` refuses all five (`08_UI` §Contrast).
- Do **not** set `Background` on `FluentWindow` (kills Mica). Page backgrounds transparent by default.
- **Every `Page` root sets `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`** (2026-10-07): the navigation's
  Frame passes no text colour into a page, and a text with none is black (§Gotchas).
- Exception to the no-hex rule: the chart palette — defined once per theme in ~~`Styles/ChartPalette.xaml` (two dictionaries)~~ `Styles/ChartPalette.Dark.xaml` and `ChartPalette.Light.xaml` (corrected 2026-10-04), never inline.
- Second exception (beta.23, D61): the game page's artwork band, `Controls/ArtTint` — a colour worked out in code from the
  artwork and the theme's own text and fill colours, the strongest tint at which every text on it still reads (4.5:1,
  tertiary 3:1). It follows a theme switch through a **resource reference on the band** (`SetResourceReference` on a
  private attached property bound to `ApplicationBackgroundColor`), not through `ApplicationThemeManager.Changed`, which
  only `ApplicationThemeManager.Apply` raises — a page made and switched in the tests' harness would never hear it.
- The change markers (beta.30, D70) take the palette's `Chart_StutterPso` under `ChartTheme.TextOn` — black or white by
  WCAG contrast, black in both themes. The chart reads `ChartTheme.Current` at every render; the WPF chip in the list
  (`Controls/ChangeBadge`) follows a switch through the same resource reference as `ArtTint` and `FgMixBar`
  (`ChartPalette.ForBackground`). No colour in XAML.
- Third exception (beta.35, D91): **a Windows contrast theme's colours**, read from `SystemColors` in one place
  (`Services/ContrastPalette.FromSystem`) and laid over the theme by `Services/ContrastOverlay` — never written in XAML. A
  colour the App works out itself reads `Services.ContrastTheme.Active` first (the charts, the badges, the artwork band);
  XAML that must change with it binds `{Binding Path=(services:ContrastTheme.IsOn)}`, a static property WPF hears through
  its `StaticPropertyChanged` event.
- **`ui:Button Appearance="Danger"` / `Info` / `Success` / `Caution` / `Dark` / `Light` paint fixed colours** — values in
  WPF UI 4.3.0's button style (`#F44336` for Danger), not resources, so no theme and no contrast theme reaches them, under
  the theme's button text (white on the red in the dark theme, 3.68:1). Danger is the one the App uses: its implicit
  `ui:Button` style (`Styles/FrameLedger.xaml`) puts the theme's dark text on the red, and the theme's own button under a
  contrast theme — message boxes' buttons included. The other five are refused
  (`ContrastThemeTests.NoButtonOrBadgeTakesAnAppearanceThatPaintsAFixedColour`).
- **A snackbar's appearances are fixed colours too** (`ISnackbarService.Show`): Success, Caution, Info, Danger, Primary,
  Dark and Light are values under white or black text — Success 2.78:1, Caution 2.16:1 in both themes. Only Secondary is
  the theme's: `Services/SnackbarStrip`, the one place a snackbar is shown, uses it for every message.
- **A `ui:Badge` with a BOUND palette appearance** is the same hole as a literal one, and no XAML scan of literals sees it
  (the agent's pill, beta.35: `AgentPill` now). A bound badge appearance is refused by the same test.

## FlowDocument (rendered documents, beta.12)

- **WPF UI 4.3.0 styles no `FlowDocument`.** Its own defaults are black Georgia on white, so a document in the dark theme
  is unreadable unless the document is themed in code: `Markdown/MarkdownRenderer` sets `TextElement.Foreground` to
  `TextFillColorPrimaryBrush` and `TextElement.FontFamily` to `ContentControlThemeFontFamily` **by resource reference**
  (`SetResourceReference`, the code form of `DynamicResource`), a transparent background, and resource references for
  every other colour (`AccentTextFillColorPrimaryBrush` links, `SubtleFillColorSecondaryBrush` code and table headers,
  `ControlStrokeColorDefaultBrush` rules, quote bars and table borders). Never a literal brush.
- Host it in `Controls/MarkdownView` (a `FlowDocumentScrollViewer`, toolbar hidden) — not a `RichTextBox`, which is an
  editor. A `Hyperlink` there does not navigate by itself: the view handles `RequestNavigate` and decides
  (`Markdown/MarkdownLinks`).
- `AccentTextFillColorPrimaryBrush` is the light accent in the dark theme only after `ApplicationThemeManager.Apply(…,
  updateAccent: true)` — measured 2026-10-03 in the test host, which skips it: links drew #003E92 on #202020 there and
  #73C2FF once the accent was applied as `WpfThemeApplier` applies it.

## Custom controls (`FpsReadout`, `TriStateChip`, sparklines)

- Derive from `Control` with `ControlTemplate` in `Styles/FrameLedger.xaml`; template uses only theme brushes above → they re-theme for free.
- `TriStateChip`: `Border` CornerRadius 12, states — Yes: `AccentFillColorDefaultBrush` bg + on-accent text; No: transparent bg + `ControlStrokeColorDefaultBrush` 1px border; N/A: dashed `StrokeDashArray="2 2"` border + `TextFillColorTertiaryBrush` text.
- **Built 2026-09-14 (P3 PR-5):** both derive from `Control` with one dependency property, `Model` (`FpsReadoutModel` / `TriStateChipModel`, decided by `Services.FpsPresentation` and the view models — the control renders, it never decides), templates in `Styles/FrameLedger.xaml` with `DataTrigger`s on the model's flags; the dashed N/A outline is a `Rectangle` behind the `Border` (a `Border` cannot dash). The source suffix icon became the tooltip: the Fluent System Icons names for 🔍/✎/↧ are not certain in this pinned version and a wrong `SymbolRegular` is a build error. The override flyout is PR-6's.
- No `SystemColors.*` anywhere.
- `ChangeBadge` (beta.30): a `Control` with one `Text` property, not focusable (the row's text carries the meaning), its
  template a `Border` and a `TextBlock` bound to `Background`/`Foreground`, which the control sets in code from the chart
  palette (`Styles/FrameLedger.xaml` writes no colour).

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

  ⚠ Name clash with `System.Windows.MessageBox` — in App code, `using MessageBox = Wpf.Ui.Controls.MessageBox;` ~~and ban the System one (BannedSymbols/analyzer note)~~ *(corrected 2026-10-04: nothing bans it. Three paths use the Win32 box on purpose — the crash dialog, the startup-failure dialog and the uninstall question — because they run when the shell's dialog host cannot be trusted. Use the WPF UI one everywhere else.)*
- Rich in-flow dialogs: `IContentDialogService` (host wired in MainWindow — a `ui:ContentDialogHost`, since 4.3.0 marks `SetDialogHost(ContentPresenter)` obsolete).
- Transient in-app: ~~`ISnackbarService.Show(title, message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(4))`~~
  `IMessageStrip` (`Info` / `Success` / `Warn`) — `Services/SnackbarStrip` is the one caller of `ISnackbarService.Show`, and
  says each message to a screen reader (beta.35, D89). A view model never holds `ISnackbarService`.
- A change a screen reader must hear goes through `IAnnouncer` (`Services/UiaAnnouncer`, UI Automation's notification
  event). **`AutomationProperties.LiveSetting` alone says nothing in WPF**: the app must raise `LiveRegionChanged` itself,
  and no XAML of the App's sets one (beta.35).
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
>
> **Accessible charts, beta.35 (D90).** Every plot in XAML is `charts:ChartPlot` (a `WpfPlot` subclass; ScottPlot's
> default style and input stay) with `AutomationProperties.Name` and `HelpText` from `A11y_Chart_*` / `A11y_ChartData_*`.
> `ChartTheme.Attach` adds `Charts/ChartKeys` (arrows and A marked handled after ScottPlot's class handler has acted — an
> instance handler runs after it) and `ResetOnDoubleClick` (`DoubleLeftClickBenchmark(false)` and a `DoubleClickResponse` on
> the left button that autoscales). A series takes `ChartPatterns.Line` / `Points` / `Hatch` by its index — a new series
> without one is red in `NoChartTellsTwoSeriesApartByColourAlone`. Under a Windows contrast theme `ChartTheme.Current` is
> `ChartPalette.ForContrast(ContrastTheme.Active)` — the theme's window under every plot, its text, link, highlight,
> button text and disabled text for the rest — and the patterns carry the series apart (D91).
>
> **The trend's change markers, beta.30 (D70):** `Charts/ChangeMarkerPlottable`, an `IPlottable` + `IRenderLast` added
> with `Add.Plottable` (which assigns no axes: the chart sets `Axes.XAxis`/`YAxis` itself), draws the dashed lines in
> `Render` — clipped to the data area — and the numbered badges in `RenderLast`, after the panels and unclipped, in a
> band kept with `plot.Axes.Top.MinimumSize` (the top axis is unused and measures 15 px; `MinimumSize` clamps it up, and
> `Clear` keeps it, so `Show` sets it every time — 0 with no marker). `LabelStyle.Measure` sizes a badge with a
> `Paint.NewDisposablePaint()`; `Charts/ChangeBadgeLayout` places them. `GetAxisLimits` returns
> `AxisLimits.HorizontalOnly`, so the markers count toward the date axis's autoscale as `VerticalLine`s did.

## Gotchas checklist

- [ ] Dictionaries order: `ThemesDictionary` → `ControlsDictionary` → app styles. Wrong order = default-looking controls.
- [ ] **A ScottPlot `VerticalLine` label grows into a panel nothing sizes for it (5.1.59).** With `LabelOppositeAxis` it is
  anchored 5 px above the data area, its box (text + 5 px padding each side) growing up into the top-axis panel, which
  an unused axis measures at 15 px; clipping is off, so the control's edge cut it — the owner's "Driver GPU: … → …",
  beta.30. `LabelAlignment` is overwritten each frame (only `ManualLabelAlignment` holds), and
  `LabelStyle.LastRenderPixelRect` includes the 3 px default `ShadowOffset` even with no shadow. The trend draws its
  own badges now (§ScottPlot theme sync).
- [ ] **`WpfPlot.DisplayScale` is read once, in the constructor**, from `VisualTreeHelper.GetDpi` before the control has
  a window. Read a pointer with `GetPlotPixelPosition(e)` (the element's DPI now); ScottPlot's pixels are device
  pixels while no `Plot.ScaleFactor` is set, and none is.
- [ ] **WPF UI 4.3.0's high-contrast theme cannot be applied as it is** (beta.35, D91). `ApplicationTheme.HighContrast` has
  no dictionary (the watcher picks HC1/HC2/HCBlack/HCWhite by Windows' theme name); each of those holds 84 `#FFFF0000`
  placeholders among 97 colours and 17 brushes (`SystemFillColor*Brush`, `MenuBar*`) that throw when the file is read
  alone — inside the App their forward references fall back to the application's resources and read as the Fluent
  theme's fills; their colours are Windows 11's defaults, not the user's; their text on the accent is the window text.
  Never let `SystemThemeWatcher` watch while a contrast theme is on: it would apply them. The App reads them for each
  key's role only (§Theming & brushes; `08_UI` §Contrast).
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
- [ ] **What a screen reader is given is the UI Automation tree, and WPF builds it with four holes** (beta.35, D88;
  `PagesLoadTests.Automation` sweeps every surface for each). (1) **A control with no automation peer** — a plain `Control`
  or `ContentControl`, WPF UI's `Image`, `Card`, `Badge`, `InfoBar` (measured on .NET 10.0.14) — is skipped by the tree; if
  it is also focusable (a `Control` is, by default) it is a Tab stop Narrator says nothing about. Give a display-only
  control a peer (`Controls/SpokenTextAutomationPeer`) and take it out of the tab order; `ui:CardControl` has a peer with an
  empty name even with a string `Header` — the App's implicit style takes it out of the tab order. (2) **A list's,
  drop-down's or grid row's item is named by its `ToString()`**: a record's is its printed form (`Choice { Value = dark,
  Label = Dark }`), a class's its full type name. Every item type overrides it (`Services/SpokenText` builds the words).
  (3) **A `TextBlock` written straight into a `ControlTemplate` is no control element** (`TextBlockAutomationPeer`: not when
  its templated parent is something other than a `ContentPresenter`): draw a template's text through a `ContentPresenter`
  (the App's InfoBar template), or give the control a peer that says it. (4) **A `DataGridTemplateColumn`'s cell has no text
  of its own**, so WPF names it `Item: <row>, Column Display Index: n`: name it with `controls:CellName.Path` (a cell style
  based on `DefaultDataGridCellStyle`, the one WPF UI's grid gives every cell). Also: a plain `ItemsControl` is focusable
  (the App's implicit style says no), and WPF UI's `CardExpander` leaves its expand button (`ExpanderToggleButton`)
  unnamed (`Controls/ExpanderToggleName`, set by the App's implicit CardExpander style).
- [ ] **WPF UI 4.3.0 keys its DataGrid parts' styles and defines no implicit one** (`DefaultDataGridRowStyle`,
  `DefaultDataGridCellStyle`, `DefaultDataGridColumnHeaderStyle`, handed to the grid by its implicit `DataGrid` style's
  `RowStyle`/`CellStyle`/`ColumnHeaderStyle` setters). A local `RowStyle` replaces WPF UI's; based on
  `{StaticResource {x:Type DataGridRow}}` it falls through to Aero2's row, whose `SystemColors.WindowBrush` no theme
  reaches: the import list was white text on white in the dark theme (owner's screenshot, beta.14). Base it on
  `DefaultDataGridRowStyle`. `AccessibilityTests.NoStyleIsBasedOnAnImplicitStyleTheThemeDoesNotDefine` refuses any
  `BasedOn` a type the App's dictionaries hold no style for, and `ImportReviewRenderTests` measures the rendered rows.
- [ ] **A style of the App's for a WPF UI control replaces the style that holds its template** (beta.23). WPF UI 4.3.0 gives
  its controls their templates in its own styles (`DefaultImageStyle`, …), applied only to a control with no style of its
  own: a local `<Style TargetType="ui:Image">` with no `BasedOn` leaves the control with no template, and it takes its
  place and draws nothing — the game page's cover was an empty space beside the name while the banner above it, with no
  style, drew (caught in a render before it shipped). Base it on WPF UI's (`BasedOn="{StaticResource DefaultImageStyle}"`).
  `AccessibilityTests.NoStyleOfAWpfUiControlLeavesOutItsTemplate`, `PagesLoadTests.TheGamePagesArtworkIsDrawn`.
- [ ] **WPF UI's `CheckBox` is `MinWidth` 120 with an 11 px padding**: in a narrow grid column it draws a sliver of the box.
  `MinWidth="0" Padding="0"` there. A `DataGridCheckBoxColumn` uses WPF's own element style, an Aero2 box — use a template
  column (`AccessibilityTests.NoDataGridCheckBoxColumn`).
- [ ] **Every page sets `ScrollViewer.CanContentScroll="False"`.** `NavigationViewContentPresenter` changes that property's
  default to true for `Page` and, on each navigation, wraps a page that keeps it in a `DynamicScrollViewer`. A page with
  its own root `ScrollViewer` inside that wrapper has infinite height, nothing to scroll — and a stock `ScrollViewer`
  marks every wheel handled even then, so the wheel scrolled nothing on five pages until beta.14 (owner: "I have to drag
  the scroll bar"); the Logs tail grew to hold every line and never followed the end.
  `AccessibilityTests.EveryPageOptsOutOfTheDynamicScrollViewer`, `PagesLoadTests.ANavigatedPageIsNot…`.
- [ ] **A stock `ScrollViewer` nested in a scroller takes the wheel at its ends too.** Inside a dialog use
  `ui:PassiveScrollViewer`, which hands the wheel on when it cannot scroll (`AccessibilityTests.NoPlainScrollViewerInsideADialog`).
  A `ContentDialog` whose content should not scroll as a whole sets `ScrollViewer.VerticalScrollBarVisibility="Disabled"`
  on the dialog: its content row then has the window's height, and a `*` row inside it can hold a list that scrolls alone
  (`ImportReviewPrompt`).
- [ ] **A `ScrollViewer` moves a FIXED 48 DIP per wheel message, whatever its delta** (`ScrollViewer._mouseWheelDelta`,
  read from .NET 10.0.12; measured on a `FlowDocumentScrollViewer`: deltas of −1, −15, −120 and −360 each moved 48). It
  ignores Windows' *lines to scroll*, and a precision touchpad's small deltas each jump the whole step — a long document
  read as slow and jerky while its layout cost about a millisecond a step. `Controls/MarkdownView` handles
  `PreviewMouseWheel` itself (beta.21): the delta in proportion at Windows' lines, eased, unhandled at its ends.
- [ ] **Microsoft.Data.Sqlite completes its "async" calls synchronously** (measured 2026-10-14, beta.23): a
  `ConfigureAwait(false)` after a ledger read does not leave the thread that asked, so a view model's load, a session's
  decode, a sort over its frames all ran on the UI thread. Work that must not run there goes in `Task.Run` —
  `Charts.SessionSeriesLoader`, the game page's executable check (`spike-notes` §21).
- [ ] **A transient page's view model is never `IDisposable`** (beta.23). Pages and their view models are transient, and
  the root container keeps every transient disposable it makes until the App exits: every Dashboard and Logs page ever
  opened stayed in memory, and each Dashboard stayed subscribed to the Agent — at a session's end every one reloaded its
  lists on the UI thread and raised its own notice. Listen on Loaded and stop on Unloaded (`Attach`/`Detach`, as Games,
  the game page and the Dashboard do); `DashboardViewModelTests.ADashboardOffScreenHearsNothing…`.
- [ ] **ScottPlot 5 zooms on every wheel and never marks it handled.** Without `Charts/ChartWheel` a wheel over a chart
  zoomed it and scrolled the page at once; a plain wheel is the page's, Ctrl+wheel the chart's, for every plot that
  `ChartTheme.Attach` registers.
- [ ] **A scroll bar's visibility is bound inside its scroller's template** (`ComputedVerticalScrollBarVisibility`), so a style
  on `ScrollBar` or `ui:DynamicScrollBar` cannot hide it. Set the scroller's own `ScrollBarVisibility` to `Hidden` — no
  bar, and the wheel, the keyboard and touch still scroll (`ui:PassiveScrollViewer` treats only `Disabled` as "cannot
  scroll") — as `Services.ScrollBarHiding` does for `ui.hide_scrollbars` (beta.14), reaching each scroller from a class
  handler on `ScrollChanged` since beta.17 rather than from `Loaded` alone (next item).
- [ ] **A class handler on `Loaded` is not a load hook.** WPF raises Loaded only on an element whose subtree listens for it
  with a handler of its own — an instance handler, a style's `EventSetter`, a template's trigger
  (`BroadcastEventHelper.OnBroadcastCallback` walks only into `SubtreeHasLoadedChangeHandler`;
  `FrameworkElement.ThisHasLoadedChangeEventHandler`) — and `EventManager.RegisterClassHandler` counts as none of them.
  WPF UI 4.3.0's `ToggleSwitch`, `CheckBox` and `ProgressRing` templates carry a Loaded trigger, so a scroller over those
  loads and a scroller over plain cards and text never does: `ScrollBarHiding` hid Settings' bar and not the Dashboard's
  (beta.17). To reach every instance, hook an event the element raises itself — a scroller's `ScrollChanged` comes from its
  first layout. `ScrollBarHidingTests.WpfRaisesLoadedOnlyWhereASubtreeListensForIt` keeps the behaviour visible, and a test
  that raises Loaded with `RaiseEvent` proves the handler, never that it is reached.
- [ ] **No dialog in 4.3.0 closes on Esc.** Neither `ContentDialog.cs` nor `MessageBox.cs` at the 4.3.0 tag handles a
  key (read 2026-09-15), and `ContentDialog.CloseButtonText` DEFAULTS to "Close" — an empty string is how a dialog
  says it has no Close button. `MessageBox.Close()` is `[Obsolete("Use Close with MessageBoxResult instead")]` while no
  such overload is public; the Close button's own path is `TemplateButtonCommand.Execute(MessageBoxButton.Close)`.
  `Services/DialogKeyboard` registers one class handler on `Window` (bubbling `KeyDown`, so a dropdown's Esc wins) and
  closes through those paths; `AccessibilityTests.EscapeClosesTheOpenDialogWithNoResult` holds it (`08_UI` §Accessibility).
  *(Corrected 2026-10-26, beta.30: the CODE handles no key, but `ContentDialog.xaml` does route them — each template
  button's `IsDefault` follows `DefaultButton`, Close is `IsCancel`, and while the primary is disabled a trigger makes
  Close the default. Enter therefore clicks the default button from a single-line text box: the consent dialog's
  `DefaultButton = Close` declined a typed phrase on Enter (the owner's report; D69 made the primary the default and the
  content keeps Enter until the phrase matches). Esc through `IsCancel` is read from the template, not measured —
  `DialogKeyboard` handles it first.)*
- [ ] **`ui:TextBlock Appearance` keeps the theme a text was made under (4.3.0).** `TextBlock.OnAppearanceChanged` stores
  `TryFindResource(…)` in the internal `AppearanceForeground`, and `TextBlockMetadata` coerces every TextBlock's `Foreground`
  to it — the brush found when the element was made, never looked up again (read at tag 4.3.0, 2026-10-07; `main` is the
  same). After a theme switch the page on screen kept the old theme's secondary text: light grey on white, dark grey on dark
  (the owner's screenshots of Settings, beta.17). The property also inherits, so the attached form on a container pins every
  text under it. Muted text takes `Style="{StaticResource SecondaryText}"` or `TertiaryText` (`Styles/FrameLedger.xaml`,
  `DynamicResource`). `ContrastTests.NoTextBlockTakesItsColourFromAppearance` refuses every form in the App's XAML and code,
  `PagesLoadTests.EveryTextFollowsAThemeSwitch` renders every surface through a switch both ways, and
  `PagesLoadTests.WpfUisAppearanceStillKeepsTheThemeATextWasMadeUnder` turns red the day upstream follows a switch.
- [ ] **A page in the `NavigationView` inherits nothing, its text colour included.** `NavigationViewContentPresenter` is a
  `Frame`, and `Frame` sets `InheritanceBehavior.SkipToAppNow`: the window's `Foreground` stops there, and a text that sets
  no colour falls to the TextBlock default, which WPF UI 4.3.0's `TextBlockMetadata` makes `Brushes.Black`. Cards, grids
  and controls set their own colour in their templates; the bare texts did not — every page's title and section headings
  were black on the dark window, 1.3:1 (the owner's screenshots of beta.17; beta.16's show it too). It came and went: a
  page on screen during a theme switch did take the window's new colour through the switch, and every navigation makes a
  new page that does not. Every `Page` root sets `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`
  (`AccessibilityTests.EveryPageSetsItsOwnTextColour`). A check that lays a page out straight in a window measures the
  window's colour, not the App's — beta.17's theme-switch check did and was green over the black titles — so the rendered
  checks host a page in the presenter, and `PagesLoadTests.EveryTextIsReadableInBothThemes` measures every surface so
  hosted at ≥ 3:1 in both themes.

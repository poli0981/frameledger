# App UI development

The screens are specified, and described as built, in [`docs/08_UI.md`](../docs/08_UI.md); the rules for the UI library
are [`docs/16_WPFUI_SYNTAX.md`](../docs/16_WPFUI_SYNTAX.md). The App is `src/FrameLedger.App`: WPF on .NET 10 with
WPF UI 4.3.0 (pinned), CommunityToolkit.Mvvm, ScottPlot for charts and Markdig for documents.

## How it is put together

- A .NET Generic Host with dependency injection. Pages and their view models are transient — a page is made again on
  every navigation, so state lives in the view models' services and the database, never in a page.
- Navigation goes only through WPF UI's `INavigationService`; a page implements `INavigableView<TViewModel>`.
- The App opens `ledger.db` itself to read; anything that acts on the PC — hooking, deleting sessions, pausing — is a
  request to the agent over the pipe.
- `FrameLedger.exe --data-dir <copy>` is a viewer: no agent, no update, the controls that would change the PC disabled.

## Strings

Every user-visible string comes from `.resx`, in English, Vietnamese and Japanese (`src/FrameLedger.App/Strings.resx`
and its `.vi` and `.ja` siblings; the shared ones are in `src/FrameLedger.Shared`). `tools/resx-gen.ps1` writes the
accessor class, and `tools/resx-audit.ps1` fails the build when a key is missing in a language. The safety texts in
Japanese wait for a human reviewer, never a machine draft ([`docs/09_I18N.md`](../docs/09_I18N.md)).

## Colours and theme

Colours come only from WPF UI's theme brushes, by `DynamicResource` — never a hex value, never `StaticResource` for a
theme brush. Muted text uses the App's `SecondaryText` and `TertiaryText` styles (`src/FrameLedger.App/Styles/FrameLedger.xaml`).
The chart palettes are the one place a colour is written out, once per theme.

## The traps of WPF UI 4.3.0

Each of these cost a release; the full list, with the tests that hold each, is §Gotchas checklist of
[`docs/16_WPFUI_SYNTAX.md`](../docs/16_WPFUI_SYNTAX.md).

- `ui:TextBlock Appearance=` looks its brush up once: a theme switch leaves the text in the old theme's colour.
- A class handler on `Loaded` does not reach every element: WPF raises Loaded only where a subtree listens for it.
- Every page sets `ScrollViewer.CanContentScroll="False"`, or WPF UI wraps it in a scroller that eats the mouse wheel;
  a scroller inside a dialog is a `ui:PassiveScrollViewer`.
- A scroll bar is hidden through its scroller's own `ScrollBarVisibility`, never a style on the bar.
- `ui:InfoBar` draws no content (the App's copy of its template does); `ui:Badge` palette appearances are not theme
  colours; WPF UI's DataGrid parts have keyed styles only; its CheckBox is 120 px wide at least.
- No dialog closes on Esc by itself (`Services/DialogKeyboard` does it); ScottPlot zooms on every wheel unless told not to.
- Apply the theme before the first window loads; watch the system theme only once the window has a handle.

## Testing the UI

`tests/FrameLedger.App.Tests` renders every page, window and dialog under the real theme dictionaries on one STA thread
(`PagesLoadTests.OnStaAsync`): contrast in both themes, a theme switch while a surface is on screen, scroll bars with the
option on in a real window, keyboard and automation names. A rendered check is run red against the unfixed XAML first.

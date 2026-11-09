# Accessibility

FrameLedger should be usable by everyone who plays games on a Windows PC — with a keyboard alone, with a screen reader,
with a Windows contrast theme or Magnifier, and without telling colours apart. This page says what FrameLedger aims
for, what works today, how that was checked, what does not work yet and what to do instead, and how to tell us about a
barrier. It is written for the person who has just met one; the details for developers are in
[`docs/08_UI.md`](docs/08_UI.md#accessibility).

FrameLedger is a desktop program: its own window, its dialogs, its document windows, the icon by the clock and the
notices Windows shows for it. It draws nothing inside a game, so it cannot make a game itself any easier to use.

*Beta software: this page describes version 0.1.0-beta.35. In the app: Help ▸ Accessibility.*

## What FrameLedger aims for

- **WCAG 2.2 at level AA**, applied to desktop software as W3C's
  [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/) guidance explains. This is a goal that guides the work, not a claim:
  no formal evaluation has been made, and FrameLedger does not say it conforms to any standard.
- **Every task can be done with the keyboard alone.**
- **A screen reader can read every control and every value**, in the language FrameLedger is shown in, and hears what
  changes — a notice, a result.
- **Text and controls keep their contrast** in the light and the dark theme and under Windows contrast themes, and
  **colour is never the only way something is shown.**
- **Everything stays sharp at any display scale**, and when the window moves between screens.

## What works today

### The keyboard

| Keys | What they do |
|---|---|
| **Ctrl+1** … **Ctrl+5** (also with the numeric keypad) | Open Dashboard, Games, Compare, Logs and Settings, and move the keyboard into that page; a screen reader hears its title. |
| **Alt** or **F10**, then the arrow keys and **Enter** | Move to the menus. **Alt** with a menu's underlined letter opens it (*File*, *Tools*, *Help* in English). |
| **Tab** and **Shift+Tab** | Move through a page or a dialog in reading order. |
| The **arrow keys**, **Home**, **End**, **Page Up**, **Page Down** | Move inside a list or a table — a game's sessions, the games list, the Compare list, the import checklist. |
| **Enter** or **Space** | Press the button, switch or box that has the focus. **Enter** on a game in the list opens its page. |
| **Esc** | Closes a dialog the way its **Close** or **Cancel** button does: nothing is changed. A dialog that has no Close button waits for an answer, and the terms window has its own **Decline** and **Accept**. |
| **Enter** in the hooking warning | Switches hooking on once the phrase is typed exactly; before that it does nothing. |
| On a chart: the **arrow keys**, **Ctrl** with an arrow, **A** | Tab reaches every chart. The arrows move its view, Ctrl with an arrow zooms, A shows all of it again (so does a double-click); the page around it stays put. |

The same keys are in the user guide, [The screens](guide/07-screens.md#keyboard-and-mouse).

### Screen readers

- **Every page, menu, dialog and setting can be reached and used with Narrator.** The owner checked this on
  2026-11-09, keyboard only with the mouse disconnected, on 0.1.0-beta.34 and 0.1.0-alpha.3, and found no fault.
- **Every page has headings**: its title is a heading of the first level, each section one of the second, so a screen
  reader's heading keys move from section to section.
- **Every chart says what it is and where its numbers are written as text**, and which keys move it.
- **Every button, switch and box that shows no text of its own has a name**, from the same translations as the screen.
- **Lists, drop-down options and table cells are read by the words they show**, and a button repeated on each row names
  its row — *Open: 11/9/2026 14:03*, *Play: Cyberpunk 2077*, *Turn off: Cyberpunk 2077*.
- **The frame-rate readout says the game's own frames, the displayed frames and the frame-generation factor together**
  (*Native 62 FPS, displayed 118 FPS, frame generation ×1.9*) — never the larger number alone; a ray-tracing chip says its
  value and where it came from (*Ray Tracing: Yes, measured*).
- **What appears is said as it appears**: a safety notice (hooking refused, measuring stopped, hooking turned off), the
  capture agent going away and coming back, an update's steps, a short message at the bottom of the window, a notice on a
  game's page and a result of **Tools ▸ Database maintenance** — in the words shown, while FrameLedger's window is the
  active one. While a game has the screen nothing interrupts it; the notice stays in the window, and Windows' own
  notifications from the icon by the clock are read by the screen reader itself.
- **The notices above the page can be read where they are shown**, and the button that answers one says which notice it
  answers.
- **Tab goes from control to control**: pictures, lists and the rows of Settings are not stops of their own.
- **Dialogs say they are dialogs**, keep the focus inside them until they close, and then give it back to the control
  that opened them. A message box opens over FrameLedger's window, never behind it.

### Seeing the screen

- **Light, dark, or the same as Windows** (Settings ▸ Theme). FrameLedger takes every colour from the theme, and every
  text is measured against what is painted under it, in both themes.
- **Windows contrast themes** (Settings ▸ Accessibility ▸ Contrast themes) — Aquatic, Desert, Dusk, Night sky, or one
  whose colours you changed: every colour in the window becomes one of the theme's, whatever Settings ▸ Theme says, as
  soon as you turn one on, edit it or turn it off. The charts draw in its colours too, their lines and marks still told
  apart by pattern, and Settings ▸ Theme says a contrast theme is in use.
- **The colour a game's picture lends its page** is used only as far as every text on it still reads, and never under a
  contrast theme.
- **Windows display scaling** at any size, on every screen; **Magnifier** (Windows logo key + Plus) works as with any
  program.
- Labels such as *Hooking on* and *Not installed*, and the ray-tracing chips, always carry their words; a chip that reads
  *N/A* is also drawn with a dashed edge.
- **No chart tells two series apart by colour alone**: its lines are solid, dashed or dotted, its points circles,
  triangles or squares, its shaded stretches striped or dotted.

### Languages

FrameLedger is shown in English, Vietnamese or Japanese (Settings ▸ Language), and the names a screen reader reads come
from the same translations. The documents — the user guide, this page, the legal texts — are in English.

## How it is checked

- **On every build**, by the project's own tests: what a screen reader is given for every page, window and dialog —
  every place Tab stops has a name, no list or table item is read by its program name, no text is hidden from a screen
  reader inside a control, no two buttons in a list share a name; each thing that appears is said once, in its words;
  every page has one first-level heading and every section title a level; no chart tells two series apart by colour
  alone; a chart's keys move it and stay its own; every control that shows no text has a name; no colour is written
  into a screen; every text reads against its background in both themes and after a theme switch, and under Windows'
  four contrast themes and two edited ones is drawn in the theme's own colours at 4.5:1 or better; **Esc** closes the
  open dialog without an answer, and a dialog gives the focus back; **Ctrl+1** … **Ctrl+5** open the pages in their
  order and move the keyboard into them; a page slides in only while Windows' animation effects are on; the program
  declares it follows each screen's scaling.
- **By hand**: the owner's pass of 2026-11-09 above, repeated for each release that changes a screen.
- **Not tested yet**: NVDA, JAWS, Voice Access, touch, Windows 10, and other people's PCs. They should work through the
  same Windows interface Narrator uses, but nobody has checked. Contrast themes are checked by the tests above and have
  not yet been looked at on a screen.

## Known limitations

Each one with what to do instead. A barrier that is not on this list is worth reporting.

- **A chart's points cannot be read one by one.** A screen reader hears the chart's name and where its numbers are:
  a session's figures and *Statistics per series* in its summary, the table under the Compare chart, the Sessions table
  and the list of changes under a trend chart, and **File ▸ Export ▸ Session CSV** or **Session JSON**.
- **In the user guide and the other documents the app shows, headings are read as ordinary text**: Windows' document
  view gives them no heading role. Their links and their order work as on any page.
- **The short messages at the bottom of the window close after four seconds**, and cannot be read again once closed. A
  screen reader is told each one as it appears.
- **Windows' text size setting** (Settings ▸ Accessibility ▸ Text size) **is not followed.** Use display scaling
  (Settings ▸ System ▸ Display ▸ Scale) or Magnifier.
- **Pausing capture is only in the menu of the icon by the clock.**
- **The windows Windows itself shows** — choosing a file, the administrator prompt — are Windows', not FrameLedger's.

## Report an accessibility problem

1. Open an issue with the
   [Accessibility problem form](https://github.com/poli0981/frameledger/issues/new?template=accessibility.yml).
2. Say what you were trying to do and what happened instead; your FrameLedger version (Help ▸ About) and your Windows
   version; and what you use — the keyboard alone, Narrator, NVDA, JAWS, Magnifier, a contrast theme — with its version.
   The steps to get there help, and so does a screenshot or a recording, but neither is required. **You never need to
   tell us anything about yourself or a disability.**
3. If you want to attach details from your PC, **Help ▸ Report a bug** builds them into a file on your PC and shows you
   what is inside first; nothing is uploaded by itself.

**A safety notice you could not perceive** — hooking refused, measuring stopped or hooking turned off, without your
knowing — is a safety problem as well: say so in the report, and it is handled first.

### How serious it is

You do not have to choose; the maintainer sets it when reading the report.

| Severity | What it means | For example |
|---|---|---|
| **Critical** | A task cannot be done at all. | Hooking cannot be switched off with the keyboard; the terms window or the hooking warning cannot be answered; a safety notice cannot be read. |
| **Serious** | It can be done only with great difficulty or a workaround. | A value can be read only by exporting the session. |
| **Moderate** | It is tiring or inconsistent. | A name that differs from what the screen shows; an extra stop for Tab. |
| **Minor** | It barely gets in the way. | A tooltip the keyboard cannot reach, when its words are shown elsewhere too. |

### What happens next

FrameLedger has one maintainer, so there are no promised times. Every report is read and answered, without doubting what
you describe. Critical problems come before other work; a workaround, when there is one, is written on the issue; the
fix ships in a release, and the issue is closed once you say it works for you — or when a fix has been out for a while
and nobody has answered.

## Who looks after this

The project's owner, poli0981, keeps this page and the checks behind it, and reviews both for each release that changes
a screen. A change to FrameLedger that moves anything on this page changes the page in the same pull request.

## For contributors

A change to the App keeps what the tests above hold: a name, from the translations in all three languages, for every
control that shows no text; colours only from the theme. Say in the pull request what you checked with the keyboard
and with Narrator. The rules and the checks are in [`docs/08_UI.md`](docs/08_UI.md#accessibility) and
[`CONTRIBUTING.md`](CONTRIBUTING.md).

## Feedback

To improve this page, open an issue or a pull request. A barrier goes through the form above, so that it is not lost.

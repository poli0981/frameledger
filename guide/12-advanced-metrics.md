# Advanced metrics

A session's summary shows the figures most people want. **Advanced metrics** is for looking closer: everything the
graphics driver and the hardware sensors will say about your PC, kept with each session and shown on a page of its
own.

It is **off unless you switch it on**, and while it is off nothing more is read or stored.

## Switch it on

Settings ▸ Recording ▸ **Advanced metrics**. A new item, **Advanced**, appears at the end of the list on the left
(**Ctrl+6**). Sessions you record from then on keep the extra readings; a session recorded while it was off is not
filled in later.

## This PC

What your PC says right now.

- **About this PC** — the graphics card's firmware, architecture and cores, its memory sizes, the driver's package,
  what each display can do with HDR and variable refresh, and the driver's global settings. A row with a number in
  brackets unfolds into a list.
- **Right now** — every reading with the time it was read at. NVIDIA's own figures are read every second; the
  sensors (fans, voltages, loads) every five seconds, so a sensor's value can be a few seconds old, and its time
  says so.
- **Over time** — choose a reading and see its last five minutes. More than eight lines at once cannot be told
  apart, so a reading with many parts (every processor core, say) is drawn as its lowest, average and highest.

These readings are taken **only while this page is open or a game is being recorded**, and what the page shows is
not stored.

## Session

Choose a session recorded with Advanced metrics on. You see the PC as it was then, what belonged to that session
alone — whether variable refresh was in use on the display the game ran on, the colour space the game presented in,
the driver's settings for that game — the lowest, average and highest of every reading, and a chart over the
session's length.

### What the game told its upscaler

For a game FrameLedger measures **from inside** (hooking is on for it), a session recorded with Advanced metrics on
also keeps what the game itself handed to DLSS and FSR:

- **DLSS** — the output size, the model preset for each quality mode, whether the game said its colour buffers are
  HDR, auto-exposure, alpha upscaling, and the exposure values.
- **FSR** — sharpening (on or off, and how strong), the largest jitter offset, the exposure, the near and far planes,
  the **range** of the field of view over the session, the flags of the call, the frame time the game reported, and
  FSR's own video memory at the moments the game asked for it.
- **Streamline** (NVIDIA's library that games use for DLSS; since 0.1.0-beta.43) — what the game tells it about
  each frame: the largest jitter offset, the motion-vector scale and how the motion vectors are stored, the near and
  far planes and whether depth is inverted, the **range** of the field of view, the aspect ratio, whether the
  projection is orthographic, and how many times the game reset the upscaler's history (a camera cut, a loading
  screen). Where a game draws several views, these are the first view's.
- **FSR frame generation** (since 0.1.0-beta.43) — how the game configured it: on or off, whether it may run
  asynchronously, its flags, whether only generated frames are presented, and the part of the picture it generates
  in. A setting the game changed during the session reads *on and off*. This is what the game **asked for**; how
  many frames were generated is in the session's summary, as before.

- **Streamline's features** (since 0.1.0-beta.44) — what Streamline answered when the game asked about a feature:
  which version it is (Streamline's own, and the DLSS model's), and what it requires — the graphics APIs it works
  with, whether V-Sync must be off, whether hardware-accelerated GPU scheduling must be on, how many threads,
  viewports and buffers, and the Windows and driver versions Streamline found beside the ones the feature needs.
  The features carry NVIDIA's own names: `kFeatureDLSS` is DLSS, `kFeatureDLSS_G` DLSS frame generation,
  `kFeatureDLSS_RR` ray reconstruction, `kFeatureReflex` Reflex. A game asks these while it starts; many ask
  again later, and then they are read. Where a game asks only at its start, the line under the session says
  *hooked — the game asks this while it starts, and did not ask again*. The version shown is the one Streamline
  reports, which can be newer than the file in the game's folder.
- **Whether a feature is supported and loaded, and what the game did with it** (since 0.1.0-beta.45) — two more
  tables, one row for each feature and call. `kFeatureDLSS_G · slIsFeatureSupported` is Streamline's answer in
  NVIDIA's own word: `eOk` is yes, and a refusal is named (`eErrorFeatureNotSupported`, `eErrorFeatureMissing`).
  `· slIsFeatureLoaded` is *on* or *off*. `· slSetFeatureLoaded` says whether the game switched the feature *on*,
  *off* or *on and off* during the session; `· slAllocateResources` and `· slFreeResources` count how many times it
  set up and released what the feature needs (up to 15, then `≥ 15`); `slShutdown` counts how many times it shut
  Streamline down. These begin when FrameLedger is in the game: what the game did while it started is not there.
  In the games run so far every one asks whether its features are supported; the other rows appear in some games
  and not in others, and no game has shown an allocation.

These are the game's own words to the upscaler, shown in the vendor's notation (`preExposure`, preset `K`).
FrameLedger does not check what the upscaler did with them.

The last four are read from calls FrameLedger hooks **only for Advanced metrics** — ten calls in all: in a game
where no session has been recorded with the option on since it started, those hooks are not there. The line under
the session says, for each call, whether it was *read*, *hooked — the game did not make the call*, or *not hooked —
the game did not load the library that has the call*; the six calls of 0.1.0-beta.45 are named in one sentence,
under which of the three happened to each.

### What NVIDIA's driver says about each frame's stages

On an NVIDIA card, in a **Direct3D 12** game that has **NVIDIA Reflex** built in, a session with Advanced metrics
on also stores the driver's own latency report — whether Reflex is on or off in the game's menu, because a game
with Reflex marks its frames either way: how long the driver says each stage of a frame took — *simulation*,
*render submit*, *present*, *driver*, *operating system's render queue*, *GPU render*, and the *total* from the start
of simulation to the end of the graphics card's work. They are seven lines under **Latency**, one point about every
second, each the average of the frames of one reading, in microseconds. A table beside them shows the driver's
low-latency state as five switches, by NVIDIA's own names, each *On*, *Off* or *On at times, off at others*, and
`fgMultiplier`: every frame-generation multiplier the driver gave during the session (*1, 2* is a session that
generated frames at ×2 for part of it). **They are the driver's switches, not the game's menu.** `bLowLatencyMode`
is on when the game switched Reflex on *and* when **Low Latency Mode** is set for that game in the NVIDIA App —
an override there shows here, and can read *On at times, off at others* in a game whose own setting never
changed. `bUseGameSleep` says whether the game calls Reflex's sleep each frame. NVIDIA asks games to call it even
while Reflex is off, so *On* there does not say Reflex is on — `bLowLatencyMode` does; some games call it only
while frame generation is on. Frame generation needs Reflex and not the other way round: the mode can be on
with `fgMultiplier` at 1.

**The stages overlap; they are not parts that add up to the total.** And *GPU render* is the time between two
marks the driver sets, at the start and at the end of the card's work on a frame: where the card waits between
pieces of work, the waiting is in it — in one game it is longer than the whole frame. So a second chart, **the
graphics card's time per frame, as the driver works it out**, has two lines of the driver's own figures: *GPU
active render*, how long the driver says the card worked on the frame, and *GPU frame time*, from the end of one
frame's work to the end of the next. **The active time is usually the shorter of the two, and not always**: in
Cyberpunk 2077 without frame generation it is about a fifth longer than the two marks are apart, frame after
frame — the driver counts work its marks do not enclose. Both are shown as the driver gives them. A driver may
give neither; the chart is then not there, and nothing is wrong.

Three things to know about them:

- **They are the driver's numbers, not FrameLedger's.** FrameLedger does not measure these stages; it stores what
  the driver reports and has not checked it against another tool. They are not the delay from a click to the screen.
- **FrameLedger asks for them from inside the game**, about once a second — the one thing it does in a game that is
  not reading what the game itself passes along. It asks only through NVIDIA's library where the game had already
  loaded it, and never loads that library itself. A copy of the library sitting in the game's own folder (some
  mods put one there) is not the driver's, and FrameLedger does not use it.
- **It switches itself off rather than disturb the game.** If one request takes two thousandths of a second or
  more, or goes wrong, FrameLedger makes no further request until the game is closed. The line under the session
  then says *stopped*, and why; the next time the game starts it reads again. On a busy PC this can happen for a
  pause that was Windows' and not the driver's.

The line under the session says what happened: *read*; *asked, and the driver reported no frame* (the game does not
use Reflex); *not read* with the reason — the game does not present through Direct3D 12, it has not loaded NVIDIA's
library, or that library is in the game's own folder; or *stopped*.

**Many games will show nothing here**, and the line under the session says why: *the game made none of the calls
these are read from* — most games set their DLSS options through a call FrameLedger cannot see, and only games that
use FSR 3.1 or later through AMD's own library show the FSR rows — or *the session was not measured inside the game*.
It never means the game uses no upscaler; the session's summary still names the upscaler as before.

**Where the camera is, which way it looks and its matrices are never read.** The field of view is kept as a lowest
and a highest value, never as a line over time.

**A game may use a unit of its own.** NVIDIA's and AMD's libraries expect the field of view in radians, and
FrameLedger shows it that way with the degrees beside it. Some games give degrees instead (Cyberpunk 2077 does, to
Streamline): a value too large to be radians is shown as the game gave it, with a line that says *Not radians*, and
is not converted — FrameLedger shows what the game said, not what it may have meant. The number of **resets** is
the number of calls in which the game told the upscaler to drop its history; menus and loading screens raise it. A
frame-generation configuration with no flag set says *None*.

A session's summary has an **Advanced metrics…** button that opens that session here.

## Sources

Every reading FrameLedger knows of, with what it belongs to (this PC, a session, or a session over time), where it
comes from, and whether it is read. Where it is not, the reason is written out:

- **the capture agent is not running as administrator**, or **PawnIO is not installed** — the processor's, the
  memory's and the motherboard's sensors need both ([Every setting](08-settings.md), *Run the agent as
  administrator*);
- **not present on this PC** — the driver's own figures exist on NVIDIA cards only;
- **read inside a game FrameLedger measures, when the game makes the call** — what the game tells DLSS and FSR
  (above);
- **read inside a game FrameLedger measures, from the graphics driver** — NVIDIA's latency report (above);
- **not read: it would take a patch at an address found while the game runs, a function of FrameLedger's handed
  to the game in place of the vendor's, or a call the vendor forbids from outside the game's own threads** — what
  the game asked of Reflex, Reflex's state through Streamline, and what a game sets and asks through Streamline's
  own functions for a feature: DLSS frame generation's options and state, the video memory DLSS estimates for
  itself, and the options of ray reconstruction, Image Scaling and Dynamic Vibrance. No later release will read
  these;
- **the game makes this call once, before FrameLedger is inside it** — what the game started Streamline with. It is
  not read, and no later release will read it;
- **never read** — a serial number, a display's identification data, which other programs are encoding, the camera's
  position in the game. FrameLedger does not read these at all.

## Good to know

- A reading that could not be taken is **N/A**, never a guess and never a zero.
- **A drive's temperature is not read**: the sensor library cannot list drives without touching removable ones.
- None of these readings has been checked against another tool yet.
- They are not part of an exported session or a bug report.

More limits are on the [Limitations](../LIMITATIONS.md) page, under *Sensors*.

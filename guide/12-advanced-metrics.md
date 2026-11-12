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

These are the game's own words to the upscaler, shown in the vendor's notation (`preExposure`, preset `K`).
FrameLedger does not check what the upscaler did with them.

The last two are read from calls FrameLedger hooks **only for Advanced metrics**: in a game where no session has
been recorded with the option on since it started, those two hooks are not there. The line under the session says,
for each of the two, whether it was *read*, *hooked — the game did not make the call*, or *not hooked — the game did
not load the library that has the call*.

**Many games will show nothing here**, and the line under the session says why: *the game made none of the calls
these are read from* — most games set their DLSS options through a call FrameLedger cannot see, and only games that
use FSR 3.1 or later through AMD's own library show the FSR rows — or *the session was not measured inside the game*.
It never means the game uses no upscaler; the session's summary still names the upscaler as before.

**Where the camera is, which way it looks and its matrices are never read.** The field of view is kept as a lowest
and a highest value, never as a line over time.

A session's summary has an **Advanced metrics…** button that opens that session here.

## Sources

Every reading FrameLedger knows of, with what it belongs to (this PC, a session, or a session over time), where it
comes from, and whether it is read. Where it is not, the reason is written out:

- **the capture agent is not running as administrator**, or **PawnIO is not installed** — the processor's, the
  memory's and the motherboard's sensors need both ([Every setting](08-settings.md), *Run the agent as
  administrator*);
- **not present on this PC** — the driver's own figures exist on NVIDIA cards only;
- **read inside a game FrameLedger measures, when the game makes the call** — what the game tells DLSS and FSR
  (above). Some further readings inside the game **come with a later release**;
- **never read** — a serial number, a display's identification data, which other programs are encoding, the camera's
  position in the game. FrameLedger does not read these at all.

## Good to know

- A reading that could not be taken is **N/A**, never a guess and never a zero.
- **A drive's temperature is not read**: the sensor library cannot list drives without touching removable ones.
- None of these readings has been checked against another tool yet.
- They are not part of an exported session or a bug report.

More limits are on the [Limitations](../LIMITATIONS.md) page, under *Sensors*.

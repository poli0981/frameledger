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

A session's summary has an **Advanced metrics…** button that opens that session here.

## Sources

Every reading FrameLedger knows of, with what it belongs to (this PC, a session, or a session over time), where it
comes from, and whether it is read. Where it is not, the reason is written out:

- **the capture agent is not running as administrator**, or **PawnIO is not installed** — the processor's, the
  memory's and the motherboard's sensors need both ([Every setting](08-settings.md), *Run the agent as
  administrator*);
- **not present on this PC** — the driver's own figures exist on NVIDIA cards only;
- **inside the game — comes with a later release**;
- **never read** — a serial number, a display's identification data, which other programs are encoding, the camera's
  position in the game. FrameLedger does not read these at all.

## Good to know

- A reading that could not be taken is **N/A**, never a guess and never a zero.
- **A drive's temperature is not read**: the sensor library cannot list drives without touching removable ones.
- None of these readings has been checked against another tool yet.
- They are not part of an exported session or a bug report.

More limits are on the [Limitations](../LIMITATIONS.md) page, under *Sensors*.

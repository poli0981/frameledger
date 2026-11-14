# Controllers and input

With each session FrameLedger notes **what the game was played with**: a controller, the keyboard and mouse, both at
once, or nothing at all — and which controllers were connected, for how long, and how their batteries did.

It reads this **from Windows, outside the game**, so it works for every game you record, measured from inside or
not. It never reads which button, stick or key you pressed, never text, never where the mouse is, and it never sends
anything to a controller.

## Switch it off, and when it starts

Settings ▸ Recording ▸ **Controllers and input** is on unless you switch it off. Nothing is read until you have
accepted the Privacy Policy that describes it: after an update, open FrameLedger once and accept the new terms. A
session recorded before that says *Not read in this session* and is not filled in later.

## In a session's summary: Played with

- **The bar and the table** — the share of the time **the game's window was in front** that was played with a
  controller, with the keyboard and mouse, with both at once, and with no input. Time when another window was in
  front is left out: typing in a chat window is not playing with the keyboard.
- **Changes between a controller and the keyboard and mouse** — how many times you went from one to the other.
- **Controllers** — each controller of the session: its name, how it was connected (USB, Bluetooth, a wireless
  adapter), how long it was in use and how long connected, how many times it connected and dropped, and its battery:
  the state it was in longest and for how long, its first and last level, and how fast it ran down when that could
  be worked out.
- **By maker** — when controllers of two makers were used: each maker's share of the time, and how often you changed.
- **Input over the session** — a chart with one row for each way of playing and a mark for every second; dashed and
  dotted lines show when a controller connected, dropped, or its battery fell to low.
- **Controller battery** — each controller's level over the session. An Xbox controller reports four levels (empty,
  low, medium, full), drawn as steps; a DualSense reports steps of ten per cent.
- On the **Frametime** chart of a measured session, **Controller marks** draws the same connections, drops and low
  batteries across the frames — so a stutter at the moment a controller dropped can be seen as one.

## On a game's page

**Details** ends with three rows. They are evidence, not a verdict — FrameLedger does not decide whether a game
"supports controllers":

- **Controller support (store)** — what Steam lists for the game: full, partial, or nothing. Read from the Steam
  app's own file on your PC; nothing is looked up online. A game from another store reads N/A.
- **Input libraries loaded** — which of Windows' controller libraries the game had loaded the last time it was
  measured from inside. Many games load one whether they use a controller or not.
- **Usually played with** — the shares over every session of the game that recorded them.

**Trend** has two more figures, **Controller share** and **Keyboard and mouse share**, and **Compare** shows what each
session was played with.

## What it cannot tell

- It knows a controller was **in use while the game was in front** — not what the game did with it.
- The keyboard and the mouse are **one** class; FrameLedger reads only the time of the last input, never a key.
- Input a program makes — Steam Input mapping a controller to keys, a macro tool — counts as keyboard and mouse.
- **Xbox-compatible controllers and the DualSense** are read for use. Other controllers are listed with their
  connections and are not counted as in use.
- A battery level is there only when the controller reports one to Windows: an Xbox controller on a cable reads
  *wired*, and a DualSense on Bluetooth may report none.
- When Steam Input stands in front of a controller, Windows shows it twice; FrameLedger counts it once and says so.

The full list is on [Limitations](../LIMITATIONS.md#controllers-and-input). What is read and kept is in the Privacy
Policy (Help ▸ Legal), and [Your data](05-your-data.md) says what an export carries.

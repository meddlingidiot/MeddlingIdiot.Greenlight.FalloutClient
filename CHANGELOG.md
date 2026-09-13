# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: one fallout trefoil on the desktop, lit from the Greenlight running on the
  machine. Green while everything passes, yellow while a pull request wants you, and grey when
  there is no Greenlight to ask.
- A detonation for red: flash, fireball, shock ring and a mushroom cloud that rises once and
  then stands there with embers in it, over a small red trefoil. It goes off when the status
  changes to red, not on a loop - one broken pipeline, one bomb.
- A build pulse. Everything drawn breathes while a build is running, mostly in the halo rather
  than in the symbol's own brightness, because "dimming" is uncomfortably close to "going out"
  and that already means something else here.
- Edit mode: drag the symbol anywhere, drag a corner or roll the wheel to resize it, and keep
  it with the tick or put it back with the cross. Enter, Escape, the arrow keys and a click on
  bare desktop all do what they look like they should. The file is written once, when the mode
  ends.
- A tray menu for everything the running overlay can absorb - size, glow, opacity, which part
  of the screen it may stand on, the disc behind it - each written straight back to
  `fallout.json`.
- "Start with Windows" in the tray menu, registering the stable shim beside the install rather
  than the versioned copy an update would move.

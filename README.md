# MeddlingIdiot.Greenlight.FalloutClient

A fallout trefoil sitting on your desktop that detonates when a pipeline breaks.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and a demonstration of how little an app needs to do to use it. The symbol glows **green**
while everything passes and **yellow** while a pull request wants you. When a build breaks it
goes off: a white flash, a fireball, a shock wave, and a mushroom cloud standing over a small
red trefoil, which is what stays on the desktop for as long as the pipeline is broken. While a
build is running, all of it pulsates. With no Greenlight on the machine at all the trefoil goes
grey — a green symbol on a four-minute-old snapshot would be lying to you.

The point of it is what it does **not** have. No Azure DevOps client, no GitHub client, no
token, no polling loop — everything it knows arrives through the SDK, from the Greenlight
already running on the machine. Strip out the drawing and the tray icon and the integration is
about twenty lines, all of them in [`App.cs`](Greenlight.FalloutClient/App.cs).

## Running it

```bash
dotnet run --project Greenlight.FalloutClient
```

Windows only: the click-through overlay and the work-area maths are Win32. The SDK itself is
not — it is plain .NET, and the same twenty lines work anywhere.

## Moving it about

The symbol ignores the mouse the rest of the time — a window you cannot click is a window that
never steals your caret — so moving it is a mode you turn on from the tray: **Move and resize
it…**. While it is on, the overlay answers the mouse and the widget grows a dashed frame,
four corner handles and two buttons:

- **Drag the middle** to carry it anywhere on the desktop. It is clamped so the whole of it
  stays on screen; the arrow keys nudge it a pixel at a time, Shift ten.
- **Drag a corner** to resize. The opposite corner stays where it is and the box stays square.
  The wheel over the symbol does the same thing about its middle.
- **✓** keeps the arrangement and writes it to the file. So do Enter, a click on bare desktop,
  and turning the mode off from the tray.
- **✕** puts it back exactly where it was when you turned the mode on. So does Escape.

Nothing is written to disk until the mode ends, so dragging the symbol across the desk costs
one write rather than a few hundred.

## The tray

Everything else lives on the mascot in the notification area:

- **Symbol on the desktop** — take it away and bring it back. Clicking the icon does the same.
- **Move and resize it…** — the mode above.
- **How big**, **How much glow**, **How solid** — the ordinary sizes, and how much light it
  throws onto the wallpaper.
- **Where it may stand** — the work area, or the whole screen including the taskbar.
- **Dark disc behind it** — the disc the trefoil sits on. On by default: a thin glowing symbol
  laid straight over a photograph is unreadable, and the disc is what stops the wallpaper
  deciding whether the build is passing.
- **Leave it grey when Greenlight is away** — a grey trefoil rather than nothing at all.
- **Start with Windows** — read from the registry every time it is shown, so it agrees with
  Task Manager's Startup tab rather than with what we last wrote there.
- **Edit the colours…** — opens `fallout.json`. **Reload the file** picks up hand edits without
  a restart.

Every setting is written straight back to the file, so the menu and the JSON are never two
different sets of settings.

## The file

`%AppData%\Greenlight.Fallout\fallout.json`, written with the defaults on first run. Each state
gets two colours — the symbol, and the light it throws — because a glow is not simply the
symbol at a lower alpha:

```json
{
  "Symbol": { "AnchorX": 0.9, "AnchorY": 0.16, "Size": 0.2 },
  "WhenGreen": { "Symbol": "#5CFF8A", "Glow": "#17C24B" },
  "WhenAmber": { "Symbol": "#FFD84D", "Glow": "#E39B10" },
  "WhenRed":   { "Symbol": "#FF4438", "Glow": "#B01208" }
}
```

`WhenRed` is the small badge standing under the cloud, not the cloud itself — the fire has its
own colours and they are not anybody's to change. A file that cannot be parsed falls back to
the defaults rather than refusing to start.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.FalloutClient/App.cs) | The whole Greenlight integration, and what to do when edit mode ends |
| [`FalloutScene.cs`](Greenlight.FalloutClient/FalloutScene.cs) | Where the symbol is, how far the detonation has got, what the mouse is over. No Avalonia, so it is testable |
| [`FalloutCanvas.cs`](Greenlight.FalloutClient/FalloutCanvas.cs) | The drawing: the trefoil, the bomb, and the edit chrome |
| [`FalloutWindow.cs`](Greenlight.FalloutClient/FalloutWindow.cs) | The click-through overlay, and the mouse and keyboard in edit mode |
| [`FalloutTray.cs`](Greenlight.FalloutClient/FalloutTray.cs) | The tray icon and its menu |
| [`ClickThroughNative.cs`](Greenlight.FalloutClient/ClickThroughNative.cs) | The four window styles that make it furniture, and the one edit mode takes back |

The scene is deliberately free of Avalonia so the detonation timing, the clamping and the
edit-mode geometry can be tested without a window — a save button that sits somewhere other
than where it was drawn is not something anybody would catch by looking at a screenshot.

```bash
dotnet test
```

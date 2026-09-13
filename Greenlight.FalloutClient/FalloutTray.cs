using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Greenlight.FalloutClient;

/// <summary>
/// The mascot in the notification area, and the menu hanging off him: the only part of this
/// toy a person can click without asking for it.
/// </summary>
/// <remarks>
/// <para>
/// The symbol is click-through by design — the overlay covers the whole desktop, so a window
/// that answered the mouse would be a desktop nobody could use. That leaves the tray for
/// everything: every setting in <see cref="FalloutConfig"/> that can be changed while the
/// thing is running is reachable from here, edit mode included, and each change is written
/// straight back to the file, so the menu and the JSON are always the same settings.
/// </para>
/// <para>
/// Avalonia's own <see cref="TrayIcon"/> rather than a tray library, because the sample is
/// meant to be readable — and because a sample that drags in a dependency to draw one icon is
/// making a point nobody asked for.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class FalloutTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.FalloutClient/Assets/MeddlingIdiot.ico");

    private readonly FalloutConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _running;
    private readonly NativeMenuItem _editing;
    private readonly NativeMenuItem _startup;

    public FalloutTray(FalloutConfig config)
    {
        _config = config;

        _status = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        _running = new NativeMenuItem
        {
            Header = "Symbol on the desktop",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _running.Click += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);

        _editing = new NativeMenuItem
        {
            Header = "Move and resize it…",
            ToggleType = MenuItemToggleType.CheckBox,
        };
        _editing.Click += (_, _) => SetEditing(!(IsEditing?.Invoke() ?? false));

        // Read from the registry rather than from a setting of ours, every time it is shown:
        // the user can turn this off in Task Manager's Startup tab, and a tick remembering what
        // we last wrote would then be telling them the opposite of the truth.
        _startup = Check("Start with Windows", WindowsStartup.IsEnabled, value => WindowsStartup.Set(value));

        var menu = BuildMenu();

        // The top-level items are not inside a submenu, so nothing else re-ticks them. Only the
        // startup one can actually change behind our back, but it can, and this is the moment
        // to notice.
        menu.Opening += (_, _) => _startup.IsChecked = WindowsStartup.IsEnabled();

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight fallout",
            Menu = menu,
            IsVisible = true,
        };

        // The one thing a left click can mean here. There is no main window to open, and a tray
        // icon that does nothing at all when clicked reads as a hung one.
        _tray.Clicked += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);
    }

    /// <summary>Whether the symbol is currently on the desktop.</summary>
    public Func<bool>? IsRunning { get; set; }

    /// <summary>Put the symbol out, or take it away.</summary>
    public Action<bool>? OnSetRunning { get; set; }

    /// <summary>Whether the symbol can currently be dragged and resized.</summary>
    public Func<bool>? IsEditing { get; set; }

    /// <summary>Turn edit mode on, or off. Off from here keeps the arrangement, as the tick does.</summary>
    public Action<bool>? OnSetEditing { get; set; }

    /// <summary>
    /// A setting changed that the overlay can absorb where it stands — which is all of them,
    /// because the symbol is drawn from its placement and its colours each frame.
    /// </summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>Re-read the file, for colours changed by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what the symbol is doing, in the tooltip and at the top of the menu.</summary>
    public void ShowState(BlastState state, bool building)
    {
        var running = IsRunning?.Invoke() ?? true;

        _status.Header = state switch
        {
            BlastState.Green => building ? "Greenlight: green — building" : "Greenlight: green — all passing",
            BlastState.Amber => building
                ? "Greenlight: yellow — building"
                : "Greenlight: yellow — a pull request wants you",
            BlastState.Red => building ? "Greenlight: red — rebuilding" : "Greenlight: red — a pipeline is broken",
            _ => "Greenlight not running — nothing claimed",
        };

        _running.IsChecked = running;
        _editing.IsChecked = IsEditing?.Invoke() ?? false;

        _tray.ToolTipText = running
            ? $"Greenlight fallout — {Short(state)}{(building ? ", building" : string.Empty)}"
            : "Greenlight fallout — put away";
    }

    private static string Short(BlastState state) => state switch
    {
        BlastState.Green => "green",
        BlastState.Amber => "yellow",
        BlastState.Red => "red",
        _ => "not connected",
    };

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private void SetRunning(bool running)
    {
        OnSetRunning?.Invoke(running);
        _running.IsChecked = running;
        _tray.ToolTipText = running ? "Greenlight fallout" : "Greenlight fallout — put away";
    }

    private void SetEditing(bool editing)
    {
        OnSetEditing?.Invoke(editing);
        _editing.IsChecked = IsEditing?.Invoke() ?? editing;
    }

    private NativeMenu BuildMenu() =>
    [
        _status,
        new NativeMenuItemSeparator(),
        _running,
        _editing,
        new NativeMenuItemSeparator(),
        Submenu("How big",
            Size("Small", 0.10),
            Size("Ordinary", 0.20),
            Size("Large", 0.32),
            Size("Hard to miss", 0.50)),
        Submenu("How much glow",
            Glow("None", 0.0),
            Glow("A little", 0.5),
            Glow("Ordinary", 1.0),
            Glow("Lighting up the room", 1.9)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.8),
            Opacity("Half there", 0.5),
            Opacity("Barely there", 0.3)),
        Submenu("Where it may stand",
            Area("Above the taskbar", AreaChoice.WorkArea),
            Area("The whole screen", AreaChoice.FullScreen)),
        Check("Dark disc behind it",
            () => _config.ShowDisc,
            value =>
            {
                _config.ShowDisc = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Leave it grey when Greenlight is away",
            () => _config.ShowWhenOff,
            value =>
            {
                _config.ShowWhenOff = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        _startup,
        new NativeMenuItemSeparator(),
        Item("Edit the colours…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    // ── the settings ──────────────────────────────────────────────────────────
    // Deliberately not here: where the symbol sits. That is what edit mode is for — a pair of
    // numbers between 0 and 1 is not something anybody wants to pick off a menu when they could
    // drag the thing instead.

    private NativeMenuItem Size(string header, double size) =>
        Choice(header, () => Math.Abs(_config.Symbol.Size - size) < 0.001, () =>
        {
            _config.Symbol.Size = size;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Glow(string header, double glow) =>
        Choice(header, () => Math.Abs(_config.Glow - glow) < 0.001, () =>
        {
            _config.Glow = glow;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Area(string header, AreaChoice area) =>
        Choice(header, () => _config.Area == area, () =>
        {
            _config.Area = area;
            Persist();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Each option asks the config what it should look like when the menu opens rather than
    // being ticked once at startup: the file is editable by hand and reloadable from this very
    // menu, and edit mode rewrites part of it with the mouse — so anything remembering its own
    // state would start lying almost immediately.

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private static NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        void Retick()
        {
            foreach (var item in items)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();
        }

        // Twice, because neither moment is reliable on its own: picking an option has to move
        // the tick off the old one straight away, and opening the menu has to account for the
        // file having been edited behind its back.
        foreach (var item in items) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),

            // Parked here rather than in a dictionary: the menu owns its items, and a second
            // collection to keep in step with it is a second thing to get wrong.
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private void Persist() => _config.Save();

    /// <summary>
    /// Open <c>fallout.json</c> in whatever the machine opens JSON with. Four states with two
    /// colours each is too much to put in a menu, and it is the one thing somebody will want to
    /// sit and fiddle with — which is what a file is for.
    /// </summary>
    private void EditConfig()
    {
        try
        {
            // It is written out on first run, but a deleted file should still open something
            // rather than nothing.
            if (!File.Exists(FalloutConfig.DefaultPath)) _config.Save();

            Process.Start(new ProcessStartInfo(FalloutConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. A desk toy does not get to
            // interrupt anyone over it.
        }
    }
}

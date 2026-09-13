using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.FalloutClient;

/// <summary>
/// One state's colours: the trefoil itself, and the light it throws.
/// </summary>
/// <remarks>
/// Two colours rather than one, because the glow is not simply the symbol at a lower alpha —
/// a hot core reads as lit where a faded copy of the same green reads as a printing error.
/// </remarks>
public sealed class FalloutLook
{
    /// <summary>The trefoil. Any hex Avalonia can parse — <c>#3DFF6E</c>, <c>#CC3DFF6E</c>.</summary>
    public string Symbol { get; set; } = "#3DFF6E";

    /// <summary>The halo around it, and the light it spills onto the disc behind.</summary>
    public string Glow { get; set; } = "#19C24B";
}

/// <summary>
/// The symbol, read from a JSON file the user can edit. Written out with the defaults the
/// first time it is missing, so "where do I change the green" has an answer that does not
/// involve rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and losing somebody's arrangement to a rebuild
/// would be its own small betrayal.
/// </remarks>
public sealed class FalloutConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.Fallout", "fallout.json");

    /// <summary>Where the symbol sits and how big it is. Edit mode writes straight into this.</summary>
    public FalloutPlacement Symbol { get; set; } = new();

    /// <summary>Nothing to report: gunmetal, and no light at all.</summary>
    public FalloutLook WhenOff { get; set; } = new()
    {
        Symbol = "#7A8089",
        Glow = "#3A3F47",
    };

    /// <summary>Everything passing: the trefoil lit green.</summary>
    public FalloutLook WhenGreen { get; set; } = new()
    {
        Symbol = "#5CFF8A",
        Glow = "#17C24B",
    };

    /// <summary>A pull request waiting on you: the trefoil lit yellow.</summary>
    public FalloutLook WhenAmber { get; set; } = new()
    {
        Symbol = "#FFD84D",
        Glow = "#E39B10",
    };

    /// <summary>
    /// A broken pipeline. The trefoil here is the small badge standing under the cloud, not
    /// the cloud itself — the fire has its own colours, and they are not anybody's to change.
    /// </summary>
    public FalloutLook WhenRed { get; set; } = new()
    {
        Symbol = "#FF4438",
        Glow = "#B01208",
    };

    /// <summary>
    /// How much of the screen the symbol may stand on. The work area by default, so a symbol
    /// dragged to the bottom edge does not end up over the Start button.
    /// </summary>
    public AreaChoice Area { get; set; } = AreaChoice.WorkArea;

    /// <summary>Overall opacity, for when the symbol is livelier than you want it to be.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// How far the halo reaches, as a multiple of the ordinary reach. Low is a flat sticker;
    /// high is the symbol lighting up the wallpaper around it.
    /// </summary>
    public double Glow { get; set; } = 1.0;

    /// <summary>
    /// Draw the dark disc the trefoil sits on.
    /// </summary>
    /// <remarks>
    /// On by default, and not decoration: a thin glowing symbol laid straight over somebody's
    /// photograph of a forest is unreadable, and the disc is what stops the wallpaper deciding
    /// whether the build is passing.
    /// </remarks>
    public bool ShowDisc { get; set; } = true;

    /// <summary>Draw a grey trefoil when Greenlight is away, rather than nothing at all.</summary>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>The colours for a given state. What the canvas asks, every frame.</summary>
    public FalloutLook LookFor(BlastState state) => state switch
    {
        BlastState.Green => WhenGreen,
        BlastState.Amber => WhenAmber,
        BlastState.Red => WhenRed,
        _ => WhenOff,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: this is a desk
    /// toy, and a stray comma should not cost you the whole thing.
    /// </summary>
    public static FalloutConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new FalloutConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<FalloutConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new FalloutConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a block. Neither should be a crash on the next frame.
            var defaults = new FalloutConfig();
            loaded.Symbol ??= defaults.Symbol;
            loaded.WhenOff ??= defaults.WhenOff;
            loaded.WhenGreen ??= defaults.WhenGreen;
            loaded.WhenAmber ??= defaults.WhenAmber;
            loaded.WhenRed ??= defaults.WhenRed;

            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.1, 1.0);
            loaded.Glow = Math.Clamp(loaded.Glow, 0.0, 2.5);

            // Clamped on the way in, not only on the way out. The file is hand-editable, and an
            // anchor of 12 or a size of -3 should give you a symbol you can find and drag back
            // rather than one that is somewhere off the side of the desktop.
            loaded.Symbol.AnchorX = Math.Clamp(loaded.Symbol.AnchorX, 0, 1);
            loaded.Symbol.AnchorY = Math.Clamp(loaded.Symbol.AnchorY, 0, 1);
            loaded.Symbol.Size = Math.Clamp(loaded.Symbol.Size, 0.03, 0.9);

            return loaded;
        }
        catch
        {
            return new FalloutConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A toy that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(FalloutConfig other)
    {
        Symbol = other.Symbol;
        WhenOff = other.WhenOff;
        WhenGreen = other.WhenGreen;
        WhenAmber = other.WhenAmber;
        WhenRed = other.WhenRed;
        Area = other.Area;
        Opacity = other.Opacity;
        Glow = other.Glow;
        ShowDisc = other.ShowDisc;
        ShowWhenOff = other.ShowWhenOff;
    }
}

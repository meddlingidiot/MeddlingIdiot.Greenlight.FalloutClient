namespace Greenlight.FalloutClient;

/// <summary>What the trefoil is doing, which is Greenlight's aggregate colour by another name.</summary>
public enum BlastState
{
    /// <summary>
    /// No Greenlight attached, or it has nothing to say. The trefoil goes grey rather than
    /// vanishing — a green symbol on a four-minute-old snapshot would be the toy lying, and a
    /// blank desktop looks like it crashed.
    /// </summary>
    Off,

    /// <summary>Everything passing. The trefoil glows green.</summary>
    Green,

    /// <summary>A pull request wants you. Greenlight's yellow.</summary>
    Amber,

    /// <summary>A pipeline is broken. The desk toy detonates.</summary>
    Red,
}

/// <summary>What the mouse is over, in edit mode.</summary>
public enum FalloutGrip
{
    /// <summary>Nothing. A click here means "I am finished".</summary>
    None,

    /// <summary>The symbol itself — drag to carry it around the desktop.</summary>
    Body,

    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight,

    /// <summary>The tick. Keep where it has been dragged to.</summary>
    Save,

    /// <summary>The cross. Put it back where it started.</summary>
    Cancel,
}

/// <summary>A point in the overlay's logical pixels.</summary>
public readonly record struct ScenePoint(double X, double Y)
{
    public static ScenePoint operator +(ScenePoint a, ScenePoint b) => new(a.X + b.X, a.Y + b.Y);

    public static ScenePoint operator -(ScenePoint a, ScenePoint b) => new(a.X - b.X, a.Y - b.Y);

    public static ScenePoint operator *(ScenePoint a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>A rectangle in the overlay's logical pixels.</summary>
public readonly record struct SceneRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public ScenePoint Centre => new(X + Width / 2, Y + Height / 2);

    public ScenePoint TopLeft => new(X, Y);

    public ScenePoint TopRight => new(Right, Y);

    public ScenePoint BottomLeft => new(X, Bottom);

    public ScenePoint BottomRight => new(Right, Bottom);

    public bool Contains(ScenePoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public SceneRect Inflate(double by) => new(X - by, Y - by, Width + by * 2, Height + by * 2);

    /// <summary>A square of side <paramref name="side"/> centred on a point.</summary>
    public static SceneRect Square(ScenePoint centre, double side) =>
        new(centre.X - side / 2, centre.Y - side / 2, side, side);
}

/// <summary>Where the symbol sits and how big it is.</summary>
/// <remarks>
/// A mutable class rather than a record, because edit mode drags it in place while the tray
/// menu and the file are both looking at the same instance — the same arrangement
/// <see cref="FalloutConfig"/> relies on when it writes a finished edit straight back to disk.
/// </remarks>
public sealed class FalloutPlacement
{
    /// <summary>
    /// Where the centre of the symbol sits, as a fraction of the overlay: 0 is the left or
    /// top edge, 1 the right or the bottom.
    /// </summary>
    /// <remarks>
    /// A fraction rather than a pixel so the symbol survives the screen it was placed on — a
    /// laptop undocked from a 4K monitor would otherwise find it jammed in the corner.
    /// </remarks>
    public double AnchorX { get; set; } = 0.90;

    public double AnchorY { get; set; } = 0.16;

    /// <summary>How big the box is, as a fraction of the overlay's height.</summary>
    public double Size { get; set; } = 0.20;

    public FalloutPlacement Copy() => new() { AnchorX = AnchorX, AnchorY = AnchorY, Size = Size };

    public void CopyFrom(FalloutPlacement other)
    {
        AnchorX = other.AnchorX;
        AnchorY = other.AnchorY;
        Size = other.Size;
    }
}

/// <summary>
/// Every measurement of the widget at a given overlay size: the box it occupies, the trefoil
/// inside it, the ground the cloud rises from, and the edit-mode furniture.
/// </summary>
/// <remarks>
/// Separate from both the drawing and the placement on purpose. The canvas needs it to draw,
/// and edit mode needs exactly the same numbers to work out what the mouse is over — two
/// copies of this arithmetic would be two chances for the button you can press to sit
/// somewhere other than the button you can see.
/// </remarks>
public readonly record struct FalloutLayout(
    SceneRect Box,
    ScenePoint Centre,
    double SymbolRadius,
    ScenePoint BadgeCentre,
    double BadgeRadius,
    ScenePoint Ground,
    double HandleSize,
    SceneRect SaveButton,
    SceneRect CancelButton)
{
    public SceneRect Handle(FalloutGrip grip) => grip switch
    {
        FalloutGrip.ResizeTopLeft => SceneRect.Square(Box.TopLeft, HandleSize),
        FalloutGrip.ResizeTopRight => SceneRect.Square(Box.TopRight, HandleSize),
        FalloutGrip.ResizeBottomLeft => SceneRect.Square(Box.BottomLeft, HandleSize),
        FalloutGrip.ResizeBottomRight => SceneRect.Square(Box.BottomRight, HandleSize),
        _ => default,
    };

    /// <summary>The four corners, in the order the canvas draws them.</summary>
    public static readonly FalloutGrip[] Corners =
    [
        FalloutGrip.ResizeTopLeft,
        FalloutGrip.ResizeTopRight,
        FalloutGrip.ResizeBottomRight,
        FalloutGrip.ResizeBottomLeft,
    ];
}

/// <summary>
/// The symbol itself: where it stands, what it is currently showing, how far the detonation
/// has got, and what the mouse is over while it is being moved. Deliberately free of Avalonia
/// — it is all arithmetic, so it can be tested without a window, which is the only way the
/// detonation timing and the edit-mode maths were ever going to be checkable.
/// </summary>
public sealed class FalloutScene
{
    /// <summary>Seconds for the symbol to fade out, or back in, across a change of state.</summary>
    private const double FadeSeconds = 0.35;

    /// <summary>Seconds the widget sits blank between one state going out and the next coming in.</summary>
    /// <remarks>
    /// The whole point of the beat. A trefoil that turned into a mushroom cloud by morphing
    /// would read as a glitch; a blank frame and then a detonation reads as something having
    /// happened, which is what the toy is trying to say.
    /// </remarks>
    private const double ChangeoverPause = 0.12;

    /// <summary>Seconds for the detonation to run from the flash to a standing mushroom cloud.</summary>
    private const double DetonationSeconds = 2.4;

    /// <summary>How much of the detonation the opening white flash occupies.</summary>
    private const double FlashFraction = 0.16;

    /// <summary>Seconds for one full breath of the build pulse — down and back up again.</summary>
    /// <remarks>
    /// Faster than a resting breath on purpose, and the one place this toy is allowed to catch
    /// the eye: "a build is running" is the state a person is most likely to be waiting on.
    /// </remarks>
    private const double PulseSeconds = 1.8;

    /// <summary>Seconds for the cloud to boil through one turn of its churn.</summary>
    private const double BoilSeconds = 6.0;

    /// <summary>Smallest the box may be dragged, in logical pixels. Below this there is nothing to grab.</summary>
    public const double MinimumSide = 56;

    private readonly Random _random;

    private BlastState _state = BlastState.Off;
    private double _pulsePhase;
    private double _pause;
    private FalloutPlacement? _beforeEdit;

    public FalloutScene(FalloutPlacement placement, int? randomSeed = null)
    {
        Placement = placement;
        _random = randomSeed is null ? new Random() : new Random(randomSeed.Value);
    }

    public FalloutPlacement Placement { get; }

    public double Width { get; private set; } = 1920;

    public double Height { get; private set; } = 1080;

    /// <summary>
    /// What Greenlight last said. Setting it starts a changeover: what is on screen fades out,
    /// the state is swapped while the widget is blank, and the new one fades in — and if the
    /// new one is red, detonates.
    /// </summary>
    public BlastState State
    {
        get => _state;
        set
        {
            if (value == _state) return;
            _state = value;

            // Nothing on screen to put away, so there is nothing to wait for and no blank beat
            // worth showing — this is the first snapshot after a cold start.
            if (Fade <= 0)
            {
                Shown = value;
                _pause = 0;
            }
            else
            {
                _pause = ChangeoverPause;
            }
        }
    }

    /// <summary>
    /// The state the widget is currently wearing. It lags <see cref="State"/> for as long as
    /// the changeover takes.
    /// </summary>
    /// <remarks>
    /// This is what makes a mushroom cloud fade out as a mushroom cloud. Drawing from
    /// <see cref="State"/> instead would turn the cloud green on its way out, which reads as
    /// the status having changed a third of a second before anything moved.
    /// </remarks>
    public BlastState Shown { get; private set; } = BlastState.Off;

    /// <summary>Whether the widget is mid-swap: fading out, or sitting blank before it fades in.</summary>
    public bool IsChangingOver => Shown != _state;

    /// <summary>A build is running. Everything drawn breathes — Greenlight's own rule.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>
    /// Show the symbol regardless of what Greenlight says. Edit mode turns this on: a symbol
    /// you cannot see is a symbol you cannot drag, and dragging it is the whole point.
    /// </summary>
    public bool ForceVisible { get; set; }

    /// <summary>
    /// Whether a grey trefoil is drawn when there is no Greenlight to ask.
    /// </summary>
    /// <remarks>
    /// On by default, and the same judgement the cars make by parking rather than vanishing: a
    /// blank desktop looks like the app crashed, where a grey trefoil looks like what it is.
    /// </remarks>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>Whether the widget is being moved and resized.</summary>
    public bool IsEditing => _beforeEdit is not null;

    /// <summary>
    /// How far in the widget is, 0 to 1. Animated rather than switched, because a mushroom
    /// cloud that simply appears reads as a drawing bug and not as a detonation.
    /// </summary>
    public double Fade { get; private set; }

    /// <summary>
    /// How far the detonation has got, 0 to 1: flash, fireball, rising column, standing cloud.
    /// Pinned at 0 for every state that is not red.
    /// </summary>
    public double Blast { get; private set; }

    /// <summary>The white of the first instant, 1 down to 0 across the opening sixth of the detonation.</summary>
    public double Flash => Blast <= 0 || Blast >= FlashFraction
        ? 0
        : Math.Pow(1 - Blast / FlashFraction, 2);

    /// <summary>Where the cloud is in its churn, 0 to 1. Drives the wobble on the cap and the stem.</summary>
    public double Boil { get; private set; }

    /// <summary>
    /// A small waver on the glow, well under a pixel of meaning. A perfectly steady glow looks
    /// printed on.
    /// </summary>
    public double Flicker { get; private set; } = 1.0;

    /// <summary>
    /// Where in the breath we are: 1 at the top, 0 at the bottom, and a flat 1 when nothing is
    /// building.
    /// </summary>
    public double Pulse => IsBuilding ? 0.5 * (1 + Math.Cos(_pulsePhase * 2 * Math.PI)) : 1;

    /// <summary>
    /// How bright the symbol is this frame: 1 at rest, easing down and back up while a build
    /// runs.
    /// </summary>
    /// <remarks>
    /// The depth is deliberately spent mostly on the halo rather than the symbol. The eye reads
    /// a change in size far more readily than a change in brightness, and a symbol that dimmed
    /// hard would read as going out — which already means something else here.
    /// </remarks>
    public double Brightness => 0.78 + 0.22 * Pulse;

    /// <summary>How far the halo reaches this frame, as a multiple of its resting reach.</summary>
    public double Halo => (IsBuilding ? 0.82 + 0.45 * Pulse : 1.0) * Flicker;

    public void Resize(double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
    }

    /// <summary>Move the scene on by one frame.</summary>
    public void Advance(TimeSpan elapsed)
    {
        // A frame that arrives after the machine has been asleep, or after a breakpoint, is
        // worth clamping: a two-minute step would run the whole detonation inside one frame,
        // which looks like a glitch rather than like time passing.
        var dt = Math.Clamp(elapsed.TotalSeconds, 0, 0.25);
        if (dt <= 0) return;

        _pulsePhase = (_pulsePhase + dt / PulseSeconds) % 1.0;
        Boil = (Boil + dt / BoilSeconds) % 1.0;

        if (IsChangingOver)
        {
            // Out, wait, then swap. Fading back in is the ordinary path below, on the frame
            // after the state has changed.
            if (Fade > 0) Fade = Math.Max(0, Fade - dt / FadeSeconds);
            else if (_pause > 0) _pause = Math.Max(0, _pause - dt);
            else Shown = _state;
        }
        else if (ForceVisible || ShowWhenOff || Shown != BlastState.Off)
        {
            Fade = Math.Min(1, Fade + dt / FadeSeconds);
        }
        else
        {
            Fade = Math.Max(0, Fade - dt / FadeSeconds);
        }

        // The detonation runs once and then stands there. It restarts only by leaving red and
        // coming back, which is the honest reading: one broken pipeline, one bomb.
        if (Shown == BlastState.Red && !IsChangingOver) Blast = Math.Min(1, Blast + dt / DetonationSeconds);
        else if (Shown != BlastState.Red) Blast = 0;

        Flicker = Fade > 0 ? 1 + (_random.NextDouble() - 0.5) * 0.04 : 1;
    }

    /// <summary>Bring the widget straight in, fully detonated. For entering edit mode.</summary>
    public void SnapVisible()
    {
        Shown = _state;
        _pause = 0;
        Fade = 1;
        if (Shown == BlastState.Red) Blast = 1;
    }

    // ── layout ────────────────────────────────────────────────────────────────

    /// <summary>Every measurement of the widget at the current overlay size.</summary>
    public FalloutLayout Measure()
    {
        var side = Side();
        var box = SceneRect.Square(new ScenePoint(Placement.AnchorX * Width, Placement.AnchorY * Height), side);

        // The trefoil fills the box with a margin; the cloud's ground line sits low in it, so
        // the column has somewhere to rise from and the cap has somewhere to reach.
        var symbolRadius = side * 0.42;
        var badgeRadius = side * 0.13;

        // Down and to the left, but on the disc rather than in the box's corner — the disc is
        // inscribed in the box, so a badge tucked into the corner hangs half off the edge of
        // the thing it is supposed to be standing on.
        var badgeReach = side * 0.30;
        var badgeCentre = new ScenePoint(box.Centre.X - badgeReach, box.Centre.Y + badgeReach);
        var ground = new ScenePoint(box.Centre.X, box.Bottom - side * 0.05);

        var handle = Math.Clamp(side * 0.14, 10, 22);

        // The buttons live above the box, unless there is no room above — at the top of the
        // screen they go underneath rather than off the edge, where they could not be pressed.
        var button = Math.Clamp(side * 0.26, 22, 40);
        var gap = button * 0.35;
        var buttonY = box.Y - gap - button < 0 ? box.Bottom + gap : box.Y - gap - button;

        var save = new SceneRect(box.Right - button, buttonY, button, button);
        var cancel = new SceneRect(box.Right - button * 2 - gap, buttonY, button, button);

        return new FalloutLayout(
            box, box.Centre, symbolRadius, badgeCentre, badgeRadius, ground, handle, save, cancel);
    }

    /// <summary>The box's side in logical pixels, clamped to something draggable and something sane.</summary>
    public double Side() => Math.Clamp(Height * Placement.Size, MinimumSide, MaximumSide);

    /// <summary>
    /// The largest the box may be: most of the shorter side of the overlay, and never smaller
    /// than the minimum — on a very short screen the two clamps would otherwise cross over and
    /// Math.Clamp would throw.
    /// </summary>
    public double MaximumSide => Math.Max(MinimumSide, Math.Min(Width, Height) * 0.9);

    /// <summary>
    /// What is under the mouse. Edit mode uses it for the cursor and for the drag, and both
    /// have to agree with what the canvas drew.
    /// </summary>
    /// <remarks>
    /// Buttons first, then corners, then the body. The save button sits close to the box's own
    /// corner handle at small sizes, and a tick that resized the widget instead of keeping it
    /// would be the single most annoying bug this thing could have.
    /// </remarks>
    public FalloutGrip HitTest(ScenePoint point)
    {
        var layout = Measure();

        if (layout.SaveButton.Contains(point)) return FalloutGrip.Save;
        if (layout.CancelButton.Contains(point)) return FalloutGrip.Cancel;

        foreach (var corner in FalloutLayout.Corners)
            if (layout.Handle(corner).Contains(point))
                return corner;

        return layout.Box.Contains(point) ? FalloutGrip.Body : FalloutGrip.None;
    }

    /// <summary>Carry the widget somewhere else. <paramref name="centre"/> is where its middle lands.</summary>
    public void MoveTo(ScenePoint centre)
    {
        // Clamped by the box rather than by its centre: an anchor clamped to 0–1 would still
        // let half the symbol hang off the edge of the desktop, and half a symbol is half a
        // thing to grab hold of when you want it back.
        var half = Side() / 2;

        Placement.AnchorX = Fraction(centre.X, half, Width);
        Placement.AnchorY = Fraction(centre.Y, half, Height);
    }

    /// <summary>
    /// Drag a corner. The opposite corner stays where it is, and the box stays square — the
    /// trefoil has one radius, and a rectangle would only ever mean squashing it.
    /// </summary>
    public void ResizeTo(FalloutGrip corner, ScenePoint point)
    {
        if (corner is not (FalloutGrip.ResizeTopLeft or FalloutGrip.ResizeTopRight
            or FalloutGrip.ResizeBottomLeft or FalloutGrip.ResizeBottomRight)) return;

        var box = Measure().Box;

        var anchored = corner switch
        {
            FalloutGrip.ResizeTopLeft => box.BottomRight,
            FalloutGrip.ResizeTopRight => box.BottomLeft,
            FalloutGrip.ResizeBottomLeft => box.TopRight,
            _ => box.TopLeft,
        };

        // The longer of the two reaches, so the box follows whichever way the mouse actually
        // went rather than stalling when it moves along only one axis.
        var side = Math.Max(Math.Abs(point.X - anchored.X), Math.Abs(point.Y - anchored.Y));
        side = Math.Clamp(side, MinimumSide, MaximumSide);

        var signX = corner is FalloutGrip.ResizeTopRight or FalloutGrip.ResizeBottomRight ? 1 : -1;
        var signY = corner is FalloutGrip.ResizeBottomLeft or FalloutGrip.ResizeBottomRight ? 1 : -1;

        // Size before the move, so MoveTo clamps the new centre against the new half-width
        // rather than the old one — resizing towards an edge would otherwise push the box off
        // it and then refuse to bring it back.
        Placement.Size = Height <= 0 ? Placement.Size : side / Height;
        MoveTo(new ScenePoint(anchored.X + signX * side / 2, anchored.Y + signY * side / 2));
    }

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>Remember where the widget was, so the cross has something to put it back to.</summary>
    /// <remarks>
    /// A second call while already editing is ignored rather than re-snapshotting. The tray can
    /// ask for edit mode while it is already on, and taking a fresh snapshot there would
    /// quietly turn the cross into a second tick.
    /// </remarks>
    public void BeginEdit() => _beforeEdit ??= Placement.Copy();

    /// <summary>Keep it where it has been dragged to.</summary>
    public void CommitEdit() => _beforeEdit = null;

    /// <summary>Put it back where it was when edit mode started.</summary>
    public void CancelEdit()
    {
        if (_beforeEdit is null) return;

        Placement.CopyFrom(_beforeEdit);
        _beforeEdit = null;
    }

    /// <summary>
    /// Where a coordinate sits in the overlay as a fraction, with the box kept fully on screen.
    /// </summary>
    private static double Fraction(double value, double half, double extent)
    {
        if (extent <= 0 || !double.IsFinite(value)) return 0.5;

        // A box wider than the overlay has no legal position at all, so it is centred rather
        // than clamped — Math.Clamp with a low above its high throws, and a 4K widget dropped
        // onto a netbook is an ordinary way to arrive here.
        if (half * 2 >= extent) return 0.5;

        return Math.Clamp(value, half, extent - half) / extent;
    }
}

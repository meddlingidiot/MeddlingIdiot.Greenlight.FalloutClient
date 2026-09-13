using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.FalloutClient;

/// <summary>
/// Draws the symbol. Everything comes off the scene's numbers each frame — there are no
/// controls, because a window nobody can click has no use for layout or hit testing, and edit
/// mode does its own.
/// </summary>
/// <remarks>
/// The glow is a stack of translucent ellipses rather than a radial gradient brush. It is a
/// dozen filled ellipses a frame either way, it survives a wallpaper of any colour, and it
/// does not depend on which of the gradient properties the current Avalonia calls what.
/// </remarks>
public sealed class FalloutCanvas : Control
{
    /// <summary>
    /// How many rings the halo is built from.
    /// </summary>
    /// <remarks>
    /// Rather more than it looks like it needs. Each ring is a hard-edged circle, so a handful
    /// of them at a workable alpha reads as a target painted round the symbol rather than as
    /// light — the fix is many rings, each almost invisible on its own.
    /// </remarks>
    private const int HaloRings = 24;

    /// <summary>
    /// How hot the cloud stays once the detonation is over.
    /// </summary>
    /// <remarks>
    /// Not zero, deliberately. The fire is what says "broken", and a red state can stand there
    /// for an afternoon — let it cool all the way and what is left on the desktop is a small
    /// grey cloud that reads as nothing much at all.
    /// </remarks>
    private const double EmberFloor = 0.16;

    private static readonly IBrush DiscBrush = new SolidColorBrush(Color.FromArgb(196, 14, 15, 18));
    private static readonly IPen DiscPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);

    // The fire, hottest first. A detonation drawn in one orange reads as a traffic cone.
    private static readonly Color FireCore = Color.FromRgb(255, 249, 214);
    private static readonly Color FireMid = Color.FromRgb(255, 168, 56);
    private static readonly Color FireEdge = Color.FromRgb(226, 74, 22);
    private static readonly Color SmokeLight = Color.FromRgb(122, 106, 98);
    private static readonly Color SmokeDark = Color.FromRgb(54, 48, 48);

    private static readonly IBrush EditFill = new SolidColorBrush(Color.FromArgb(30, 120, 200, 255));
    private static readonly IPen EditPen = new Pen(
        new SolidColorBrush(Color.FromArgb(210, 150, 210, 255)), 1.5, new DashStyle([4, 3], 0));

    private static readonly IBrush HandleBrush = new SolidColorBrush(Color.FromArgb(235, 245, 250, 255));
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 40, 60, 90)), 1);

    private static readonly IBrush SaveBrush = new SolidColorBrush(Color.FromArgb(240, 32, 160, 78));
    private static readonly IBrush CancelBrush = new SolidColorBrush(Color.FromArgb(240, 190, 48, 40));
    private static readonly IBrush ButtonHeld = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
    private static readonly IPen GlyphPen = new Pen(Brushes.White, 2.4)
    {
        LineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    };

    private readonly FalloutConfig _config;
    private readonly Dictionary<string, Color> _colours = [];

    public FalloutCanvas(FalloutScene scene, FalloutConfig config)
    {
        Scene = scene;
        _config = config;
        IsHitTestVisible = false;
    }

    public FalloutScene Scene { get; }

    /// <summary>Whether the move-and-resize chrome is drawn.</summary>
    public bool IsEditing { get; set; }

    /// <summary>What is currently being dragged, so it can be shown as pressed.</summary>
    public FalloutGrip Held { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Height <= 1 || Bounds.Width <= 1) return;

        var layout = Scene.Measure();

        // Faded all the way out and not being edited: nothing to draw, and nothing to spend a
        // frame on either.
        if (Scene.Fade > 0.001)
        {
            using var fade = context.PushOpacity(Ease(Scene.Fade));

            if (_config.ShowDisc) DrawDisc(context, layout);

            if (Scene.Shown == BlastState.Red) DrawDetonation(context, layout);
            else DrawTrefoilFace(context, layout);
        }

        if (IsEditing) DrawEditChrome(context, layout);
    }

    // ── the quiet states ──────────────────────────────────────────────────────

    private void DrawDisc(DrawingContext context, FalloutLayout layout)
    {
        var radius = layout.Box.Width / 2;
        context.DrawEllipse(DiscBrush, DiscPen, ToPoint(layout.Centre), radius, radius);
    }

    /// <summary>The trefoil filling the box: a halo, then the symbol, in this state's colours.</summary>
    private void DrawTrefoilFace(DrawingContext context, FalloutLayout layout)
    {
        var look = _config.LookFor(Scene.Shown);
        var symbol = Colour(look.Symbol, fallback: Colors.Gray);
        var glow = Colour(look.Glow, fallback: symbol);

        // Nothing to report throws no light. A grey trefoil with a halo would look like it was
        // claiming something.
        if (Scene.Shown != BlastState.Off)
            DrawHalo(context, layout.Centre, layout.SymbolRadius * 1.05, glow);

        var brightness = Scene.Shown == BlastState.Off ? 1 : Scene.Brightness;

        context.DrawGeometry(
            new SolidColorBrush(Fade(symbol, brightness)), null,
            Trefoil(layout.Centre, layout.SymbolRadius));
    }

    /// <summary>
    /// The light the symbol throws: concentric rings, each fainter and wider than the last.
    /// </summary>
    /// <remarks>
    /// Most of the build pulse is spent here rather than on the symbol's own alpha. The eye
    /// reads a halo swelling and settling far more readily than it reads a colour dimming —
    /// and "dimming" is uncomfortably close to "going out", which already means something else.
    /// </remarks>
    private void DrawHalo(DrawingContext context, ScenePoint centre, double radius, Color colour)
    {
        var reach = radius * (1 + 0.85 * _config.Glow) * Scene.Halo;
        if (reach <= radius || _config.Glow <= 0) return;

        var point = ToPoint(centre);

        for (var i = HaloRings; i >= 1; i--)
        {
            var t = (double)i / HaloRings;
            var r = radius + (reach - radius) * t;

            // Squared falloff, and an alpha low enough that no single ring has a visible edge —
            // what is seen is the two dozen of them piling up towards the middle.
            var alpha = 0.085 * Math.Pow(1 - t, 2) * Scene.Brightness;
            if (alpha < 0.002) continue;

            context.DrawEllipse(new SolidColorBrush(colour, alpha), null, point, r, r);
        }
    }

    // ── the loud one ──────────────────────────────────────────────────────────

    /// <summary>
    /// The broken pipeline: a flash, a fireball, a shock ring, and a column rising into a cap —
    /// with the trefoil still there, small and red, standing underneath it.
    /// </summary>
    private void DrawDetonation(DrawingContext context, FalloutLayout layout)
    {
        var side = layout.Box.Width;
        var ground = layout.Ground;
        var blast = Scene.Blast;

        // Eased so the column leaps and then settles. Linear, it rises like a lift.
        var rise = 1 - Math.Pow(1 - blast, 2.2);
        var breath = Scene.IsBuilding ? Scene.Halo : 1;

        DrawGroundCloud(context, ground, side, rise, breath);
        DrawStem(context, ground, side, rise);
        DrawCap(context, ground, side, rise, breath);
        DrawShockRing(context, ground, side, blast);
        DrawFireball(context, ground, side, blast, breath);

        // Last, so it stands in front of its own smoke: the small red trefoil. It is the part
        // that says what this is about — a mushroom cloud on its own is just weather.
        var badge = _config.WhenRed;
        var symbol = Colour(badge.Symbol, Colors.Red);

        DrawHalo(context, layout.BadgeCentre, layout.BadgeRadius * 1.1, Colour(badge.Glow, symbol));
        context.DrawGeometry(
            new SolidColorBrush(Fade(symbol, Scene.Brightness)), null,
            Trefoil(layout.BadgeCentre, layout.BadgeRadius));

        // The white of the first instant, over everything, going as fast as it came.
        if (Scene.Flash > 0)
            context.DrawEllipse(
                new SolidColorBrush(Colors.White, Scene.Flash * 0.9), null,
                ToPoint(layout.Centre), side * 0.62, side * 0.62);
    }

    /// <summary>The base surge: dust rolling outwards along the ground.</summary>
    private static void DrawGroundCloud(
        DrawingContext context, ScenePoint ground, double side, double rise, double breath)
    {
        var width = side * 0.46 * rise * breath;
        var height = side * 0.10 * rise;
        if (width <= 0 || height <= 0) return;

        for (var i = 0; i < 3; i++)
        {
            var t = i / 2.0;
            var colour = Mix(SmokeLight, SmokeDark, t);
            context.DrawEllipse(
                new SolidColorBrush(colour, 0.55 - t * 0.18), null,
                new Point(ground.X, ground.Y - height * t * 0.5),
                width * (1 - t * 0.22), height * (1 + t * 0.35));
        }
    }

    /// <summary>The column. It leans and thickens as it goes, because a straight pipe reads as a chimney.</summary>
    private void DrawStem(DrawingContext context, ScenePoint ground, double side, double rise)
    {
        var height = side * 0.50 * rise;
        if (height <= 1) return;

        var wobble = Math.Sin(Scene.Boil * 2 * Math.PI) * side * 0.02;
        var halfBase = side * 0.075;
        var halfTop = side * 0.055;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(ground.X - halfBase, ground.Y), true);
            ctx.CubicBezierTo(
                new Point(ground.X - halfTop + wobble, ground.Y - height * 0.5),
                new Point(ground.X - halfTop - wobble, ground.Y - height * 0.8),
                new Point(ground.X - halfTop, ground.Y - height));
            ctx.LineTo(new Point(ground.X + halfTop, ground.Y - height));
            ctx.CubicBezierTo(
                new Point(ground.X + halfTop + wobble, ground.Y - height * 0.8),
                new Point(ground.X + halfTop - wobble, ground.Y - height * 0.5),
                new Point(ground.X + halfBase, ground.Y));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(SmokeLight, 0.88), null, geometry);

        // The column is still lit from inside early on, and goes over to smoke as it climbs.
        var heat = Math.Clamp(1 - Scene.Blast * 1.4, 0, 1);
        if (heat > 0)
            context.DrawGeometry(new SolidColorBrush(FireEdge, heat * 0.55), null, geometry);
    }

    /// <summary>The cap: overlapping billows, hottest underneath where the column feeds it.</summary>
    private void DrawCap(DrawingContext context, ScenePoint ground, double side, double rise, double breath)
    {
        var top = ground.Y - side * 0.50 * rise;
        var radius = side * 0.30 * rise * breath;
        if (radius <= 1) return;

        // Five billows around the centre, each nudged by the boil so the cap turns over slowly
        // rather than sitting there like a drawn cloud.
        for (var i = 0; i < 5; i++)
        {
            var phase = (Scene.Boil + i / 5.0) * 2 * Math.PI;
            var spread = radius * (0.55 + 0.45 * Math.Cos(i * 1.7));
            var x = ground.X + Math.Cos(i * 2.4) * spread + Math.Sin(phase) * radius * 0.06;
            var y = top - radius * 0.12 + Math.Cos(phase) * radius * 0.05 + Math.Abs(Math.Sin(i * 1.1)) * radius * 0.12;

            var lobe = radius * (0.62 - i * 0.05);
            context.DrawEllipse(new SolidColorBrush(SmokeLight, 0.9), null, new Point(x, y), lobe, lobe * 0.82);
        }

        // The underside, still burning. This is what makes it a detonation rather than a cloud —
        // and it never goes all the way out, because the state it belongs to does not either. A
        // pipeline that is still broken an hour later has a cloud with embers in it.
        var heat = Math.Max(EmberFloor, 1 - Scene.Blast * 1.1) * Scene.Brightness;
        if (heat > 0.01)
        {
            context.DrawEllipse(
                new SolidColorBrush(FireMid, heat * 0.55), null,
                new Point(ground.X, top + radius * 0.24), radius * 0.60, radius * 0.26);
            context.DrawEllipse(
                new SolidColorBrush(FireCore, heat * 0.40), null,
                new Point(ground.X, top + radius * 0.20), radius * 0.26, radius * 0.13);
        }

        // The cap's own shadow, so it is not a flat blob of one grey.
        context.DrawEllipse(
            new SolidColorBrush(SmokeDark, 0.35), null,
            new Point(ground.X, top + radius * 0.42), radius * 0.86, radius * 0.22);
    }

    /// <summary>The blast wave: one expanding ring, gone before the cloud has formed.</summary>
    private static void DrawShockRing(DrawingContext context, ScenePoint ground, double side, double blast)
    {
        const double life = 0.5;
        if (blast <= 0 || blast >= life) return;

        var t = blast / life;
        var radius = side * (0.08 + 0.54 * t);
        var alpha = Math.Pow(1 - t, 1.6) * 0.75;

        var pen = new Pen(new SolidColorBrush(FireCore, alpha), Math.Max(1, side * 0.02 * (1 - t)));
        context.DrawEllipse(null, pen, new Point(ground.X, ground.Y - side * 0.06), radius, radius * 0.52);
    }

    /// <summary>The fireball at the foot of the column, brightest at time zero.</summary>
    private static void DrawFireball(
        DrawingContext context, ScenePoint ground, double side, double blast, double breath)
    {
        // The same embers the cap keeps, a little lower: the base of the column goes on glowing
        // for as long as the pipeline is broken. It shrinks as it cools rather than fading in
        // place — a fireball that kept its full width at a tenth of its brightness reads as fog
        // sitting over the bottom of the cloud.
        var cooled = Math.Clamp(blast * 1.6, 0, 1);
        var heat = Math.Max(EmberFloor, 1 - cooled);
        var radius = side * 0.15 * (1 - 0.5 * cooled) * breath;
        var centre = new Point(ground.X, ground.Y - radius * 0.5);

        context.DrawEllipse(new SolidColorBrush(FireEdge, heat * 0.75), null, centre, radius * 1.25, radius * 1.05);
        context.DrawEllipse(new SolidColorBrush(FireMid, heat * 0.85), null, centre, radius * 0.85, radius * 0.75);
        context.DrawEllipse(new SolidColorBrush(FireCore, heat), null, centre, radius * 0.45, radius * 0.42);
    }

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The frame, the corner handles and the two buttons. Drawn outside the faded content on
    /// purpose: the chrome has to stay solid even when the symbol underneath it is mid-change,
    /// or the tick would blink while somebody was aiming at it.
    /// </summary>
    private void DrawEditChrome(DrawingContext context, FalloutLayout layout)
    {
        var box = layout.Box;
        context.DrawRectangle(EditFill, EditPen, new Rect(box.X, box.Y, box.Width, box.Height));

        foreach (var corner in FalloutLayout.Corners)
        {
            var handle = layout.Handle(corner);
            var fill = Held == corner ? SaveBrush : HandleBrush;
            context.DrawRectangle(
                fill, HandlePen,
                new RoundedRect(new Rect(handle.X, handle.Y, handle.Width, handle.Height), 2));
        }

        DrawButton(context, layout.SaveButton, SaveBrush, tick: true);
        DrawButton(context, layout.CancelButton, CancelBrush, tick: false);
    }

    private void DrawButton(DrawingContext context, SceneRect rect, IBrush fill, bool tick)
    {
        var centre = new Point(rect.Centre.X, rect.Centre.Y);
        var radius = rect.Width / 2;

        context.DrawEllipse(fill, HandlePen, centre, radius, radius);

        if (Held == (tick ? FalloutGrip.Save : FalloutGrip.Cancel))
            context.DrawEllipse(ButtonHeld, null, centre, radius, radius);

        var r = radius * 0.48;
        var pen = new Pen(Brushes.White, Math.Max(1.6, radius * 0.20))
        {
            LineCap = GlyphPen.LineCap,
            LineJoin = GlyphPen.LineJoin,
        };

        if (tick)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(centre.X - r, centre.Y + r * 0.05), false);
                ctx.LineTo(new Point(centre.X - r * 0.25, centre.Y + r * 0.72));
                ctx.LineTo(new Point(centre.X + r, centre.Y - r * 0.66));
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            context.DrawLine(pen, new Point(centre.X - r, centre.Y - r), new Point(centre.X + r, centre.Y + r));
            context.DrawLine(pen, new Point(centre.X + r, centre.Y - r), new Point(centre.X - r, centre.Y + r));
        }
    }

    // ── the symbol itself ─────────────────────────────────────────────────────

    /// <summary>
    /// The trefoil, to the proportions the standard actually gives: a central disc of radius
    /// <c>r</c>, and three 60° blades running from <c>1.5r</c> out to <c>5r</c>, with 60° of
    /// air between them.
    /// </summary>
    /// <remarks>
    /// The gap points straight up, which is the way round everybody has seen it. Rotated by
    /// sixty degrees it is still a trefoil and still reads as radiation, but it reads as a
    /// slightly wrong one — which on a symbol this familiar is worse than a different symbol.
    /// </remarks>
    public static Geometry Trefoil(ScenePoint centre, double radius)
    {
        var unit = radius / 5;
        var inner = unit * 1.5;
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            // Screen coordinates: Y grows downwards, so 90° is straight down and the blades sit
            // at down, up-left and up-right.
            for (var blade = 0; blade < 3; blade++)
            {
                var mid = 90 + blade * 120;
                var from = mid - 30;
                var to = mid + 30;

                ctx.BeginFigure(Polar(centre, inner, from), true);
                ctx.LineTo(Polar(centre, radius, from));
                ctx.ArcTo(Polar(centre, radius, to), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.LineTo(Polar(centre, inner, to));
                ctx.ArcTo(Polar(centre, inner, from), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
                ctx.EndFigure(true);
            }

            // The disc in the middle. Two half-arcs, because a circle is not a primitive in a
            // stream geometry and this figure has to be part of the same fill.
            var left = new Point(centre.X - unit, centre.Y);
            var right = new Point(centre.X + unit, centre.Y);

            ctx.BeginFigure(left, true);
            ctx.ArcTo(right, new Size(unit, unit), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(left, new Size(unit, unit), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Point Polar(ScenePoint centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(centre.X + Math.Cos(radians) * radius, centre.Y + Math.Sin(radians) * radius);
    }

    private static Point ToPoint(ScenePoint point) => new(point.X, point.Y);

    /// <summary>Ease the fade, so a state change does not start and stop with a jolt.</summary>
    private static double Ease(double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return clamped * clamped * (3 - 2 * clamped);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        var k = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * k),
            (byte)(a.R + (b.R - a.R) * k),
            (byte)(a.G + (b.G - a.G) * k),
            (byte)(a.B + (b.B - a.B) * k));
    }

    /// <summary>Darken a colour towards nothing, for the breath. Alpha is left alone.</summary>
    private static Color Fade(Color colour, double brightness)
    {
        var k = Math.Clamp(brightness, 0, 1);
        return Color.FromArgb(colour.A, (byte)(colour.R * k), (byte)(colour.G * k), (byte)(colour.B * k));
    }

    /// <summary>
    /// A colour out of the config, parsed once and remembered.
    /// </summary>
    /// <remarks>
    /// Cached because this is asked several times a frame at 60fps, and because a hand-edited
    /// file is entitled to contain <c>"fluorescent"</c> — which must cost one failed parse and
    /// then nothing, rather than one per frame forever.
    /// </remarks>
    private Color Colour(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        if (_colours.TryGetValue(hex, out var cached)) return cached;

        var parsed = Color.TryParse(hex, out var colour) ? colour : fallback;
        _colours[hex] = parsed;
        return parsed;
    }
}

using Greenlight.FalloutClient;

namespace Greenlight.FalloutClient.UnitTests;

/// <summary>
/// The scene, without a window. Everything here is arithmetic somebody would otherwise have to
/// check by dragging a widget around a desktop and squinting at it.
/// </summary>
public class FalloutSceneTests
{
    private static FalloutScene Scene(double size = 0.2, double x = 0.5, double y = 0.5) =>
        new(new FalloutPlacement { AnchorX = x, AnchorY = y, Size = size }, randomSeed: 7)
        {
            ShowWhenOff = false,
        };

    private static FalloutScene Sized(double size = 0.2, double x = 0.5, double y = 0.5)
    {
        var scene = Scene(size, x, y);
        scene.Resize(1600, 900);
        return scene;
    }

    private static void Run(FalloutScene scene, double seconds, double step = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds; t += step) scene.Advance(TimeSpan.FromSeconds(step));
    }

    // ── states and the changeover ─────────────────────────────────────────────

    [Fact]
    public void FirstStateArrivesWithoutAChangeover()
    {
        var scene = Sized();

        // Nothing on screen to put away, so the first snapshot after a cold start is simply
        // worn rather than waited for.
        scene.State = BlastState.Green;

        Assert.Equal(BlastState.Green, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void AChangeOfStateFadesTheOldOneOutFirst()
    {
        var scene = Sized();
        scene.State = BlastState.Green;
        Run(scene, 1);

        scene.State = BlastState.Red;

        // Still green on the very next frame: the cloud must not appear on top of the trefoil.
        scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
        Assert.Equal(BlastState.Green, scene.Shown);
        Assert.True(scene.IsChangingOver);

        Run(scene, 2);
        Assert.Equal(BlastState.Red, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void RedDetonatesOnceAndThenStands()
    {
        var scene = Sized();
        scene.State = BlastState.Red;

        Assert.Equal(0, scene.Blast);

        Run(scene, 0.4);
        var early = scene.Blast;
        Assert.InRange(early, 0.01, 0.99);

        Run(scene, 4);
        Assert.Equal(1, scene.Blast);
    }

    [Fact]
    public void LeavingRedPutsTheBombAway()
    {
        var scene = Sized();
        scene.State = BlastState.Red;
        Run(scene, 4);

        scene.State = BlastState.Green;
        Run(scene, 2);

        Assert.Equal(0, scene.Blast);
    }

    [Fact]
    public void TheFlashIsOverLongBeforeTheCloudHasFormed()
    {
        var scene = Sized();
        scene.State = BlastState.Red;

        Run(scene, 0.1);
        Assert.True(scene.Flash > 0);

        Run(scene, 2);
        Assert.Equal(0, scene.Flash);
    }

    [Fact]
    public void NothingToShowFadesAllTheWayOut()
    {
        var scene = Sized();
        scene.State = BlastState.Green;
        Run(scene, 1);

        scene.State = BlastState.Off;
        Run(scene, 3);

        Assert.Equal(0, scene.Fade);
    }

    [Fact]
    public void GreyWhenOffKeepsTheSymbolOnScreen()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;

        Run(scene, 2);

        Assert.Equal(1, scene.Fade);
        Assert.Equal(BlastState.Off, scene.Shown);
    }

    // ── the build pulse ───────────────────────────────────────────────────────

    [Fact]
    public void NothingBreathesUnlessABuildIsRunning()
    {
        var scene = Sized();
        scene.State = BlastState.Green;
        Run(scene, 1);

        Assert.Equal(1, scene.Pulse);
        Assert.Equal(1, scene.Brightness, 6);
    }

    [Fact]
    public void ABuildMakesTheHaloSwellAndSettle()
    {
        var scene = Sized();
        scene.State = BlastState.Green;
        scene.IsBuilding = true;

        var low = double.MaxValue;
        var high = double.MinValue;

        // Two full breaths, sampled every frame. The halo carries most of the pulse, so this is
        // the number that has to actually move.
        for (var i = 0; i < 240; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            low = Math.Min(low, scene.Halo);
            high = Math.Max(high, scene.Halo);
        }

        Assert.True(high - low > 0.3, $"the halo barely moved: {low:F2} to {high:F2}");
        Assert.InRange(scene.Brightness, 0.7, 1.0);
    }

    // ── where it sits ─────────────────────────────────────────────────────────

    [Fact]
    public void TheBoxIsSquareAndCentredOnTheAnchor()
    {
        var scene = Sized(size: 0.2, x: 0.25, y: 0.75);
        var box = scene.Measure().Box;

        Assert.Equal(box.Width, box.Height, 6);
        Assert.Equal(180, box.Width, 6);          // 0.2 of 900
        Assert.Equal(400, box.Centre.X, 6);       // 0.25 of 1600
        Assert.Equal(675, box.Centre.Y, 6);       // 0.75 of 900
    }

    [Fact]
    public void DraggingKeepsTheWholeBoxOnTheDesktop()
    {
        var scene = Sized();

        scene.MoveTo(new ScenePoint(-500, -500));
        var box = scene.Measure().Box;

        Assert.True(box.X >= -0.001, $"left edge off screen at {box.X}");
        Assert.True(box.Y >= -0.001, $"top edge off screen at {box.Y}");

        scene.MoveTo(new ScenePoint(9000, 9000));
        box = scene.Measure().Box;

        Assert.True(box.Right <= 1600.001, $"right edge off screen at {box.Right}");
        Assert.True(box.Bottom <= 900.001, $"bottom edge off screen at {box.Bottom}");
    }

    [Fact]
    public void ResizingHoldsTheOppositeCornerStill()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        var before = scene.Measure().Box;

        scene.ResizeTo(FalloutGrip.ResizeBottomRight, new ScenePoint(before.X + 300, before.Y + 300));
        var after = scene.Measure().Box;

        Assert.Equal(before.X, after.X, 3);
        Assert.Equal(before.Y, after.Y, 3);
        Assert.Equal(300, after.Width, 3);
        Assert.Equal(after.Width, after.Height, 6);
    }

    [Fact]
    public void ResizingTheOtherWayHoldsItsOwnCorner()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        var before = scene.Measure().Box;

        scene.ResizeTo(FalloutGrip.ResizeTopLeft, new ScenePoint(before.Right - 250, before.Bottom - 250));
        var after = scene.Measure().Box;

        Assert.Equal(before.Right, after.Right, 3);
        Assert.Equal(before.Bottom, after.Bottom, 3);
        Assert.Equal(250, after.Width, 3);
    }

    [Fact]
    public void ABoxCannotBeDraggedDownToNothing()
    {
        var scene = Sized(size: 0.2);
        var box = scene.Measure().Box;

        scene.ResizeTo(FalloutGrip.ResizeBottomRight, new ScenePoint(box.X + 2, box.Y + 2));

        Assert.Equal(FalloutScene.MinimumSide, scene.Measure().Box.Width, 3);
    }

    [Fact]
    public void ABoxCannotBeDraggedBiggerThanTheDesktop()
    {
        var scene = Sized(size: 0.2);
        var box = scene.Measure().Box;

        scene.ResizeTo(FalloutGrip.ResizeBottomRight, new ScenePoint(box.X + 5000, box.Y + 5000));

        Assert.Equal(scene.MaximumSide, scene.Measure().Box.Width, 3);
    }

    [Fact]
    public void AWidgetWrittenOnABiggerScreenStillFitsOnThisOne()
    {
        // A placement written on a 4K monitor, opened on something much smaller. The size clamp
        // is what keeps this honest — without it the box would be wider than the desktop, and
        // MoveTo would be asked to clamp a position between a low above its own high.
        var scene = Scene(size: 4.0);
        scene.Resize(200, 120);

        scene.MoveTo(new ScenePoint(10, 10));
        var box = scene.Measure().Box;

        Assert.True(box.Width <= 120, $"the box is wider than the desktop at {box.Width}");
        Assert.True(box.X >= -0.001 && box.Right <= 200.001, $"off the side: {box.X} to {box.Right}");
        Assert.True(box.Y >= -0.001 && box.Bottom <= 120.001, $"off the top or bottom: {box.Y} to {box.Bottom}");
    }

    [Fact]
    public void AnOverlayWithNoRoomAtAllCentresRatherThanThrowing()
    {
        // Asked to lay out before the window has been given a size. The minimum side is bigger
        // than the whole overlay here, so there is no legal position — Math.Clamp would throw
        // on a low above its high, and this is the frame that would take the app down with it.
        var scene = Scene();
        scene.Resize(0, 0);

        scene.MoveTo(new ScenePoint(10, 10));

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);
    }

    // ── what the mouse is over ────────────────────────────────────────────────

    [Fact]
    public void TheMiddleOfTheBoxIsTheThingYouDrag()
    {
        var scene = Sized();
        Assert.Equal(FalloutGrip.Body, scene.HitTest(scene.Measure().Centre));
    }

    [Fact]
    public void BareDesktopIsNotTheWidget()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        Assert.Equal(FalloutGrip.None, scene.HitTest(new ScenePoint(10, 10)));
    }

    [Theory]
    [InlineData(FalloutGrip.ResizeTopLeft)]
    [InlineData(FalloutGrip.ResizeTopRight)]
    [InlineData(FalloutGrip.ResizeBottomLeft)]
    [InlineData(FalloutGrip.ResizeBottomRight)]
    public void EveryCornerAnswersItsOwnHandle(FalloutGrip corner)
    {
        var scene = Sized();
        var layout = scene.Measure();

        Assert.Equal(corner, scene.HitTest(layout.Handle(corner).Centre));
    }

    [Fact]
    public void TheButtonsWinAgainstAnythingUnderneathThem()
    {
        var scene = Sized();
        var layout = scene.Measure();

        Assert.Equal(FalloutGrip.Save, scene.HitTest(layout.SaveButton.Centre));
        Assert.Equal(FalloutGrip.Cancel, scene.HitTest(layout.CancelButton.Centre));
    }

    [Fact]
    public void TheButtonsMoveBelowTheBoxWhenThereIsNoRoomAbove()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        Assert.True(scene.Measure().SaveButton.Bottom < scene.Measure().Box.Y);

        // Dragged to the very top of the desktop, where buttons drawn above it would be off the
        // edge and unpressable.
        scene.MoveTo(new ScenePoint(800, 0));
        var layout = scene.Measure();

        Assert.True(layout.SaveButton.Y > layout.Box.Bottom, "the buttons stayed off the top of the screen");
    }

    // ── save and cancel ───────────────────────────────────────────────────────

    [Fact]
    public void CancelPutsItBackWhereItStarted()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(200, 200));
        scene.ResizeTo(FalloutGrip.ResizeBottomRight, new ScenePoint(600, 600));
        scene.CancelEdit();

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);
        Assert.Equal(0.2, scene.Placement.Size, 6);
        Assert.False(scene.IsEditing);
    }

    [Fact]
    public void SaveKeepsWhereItWasDraggedTo()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(200, 300));
        scene.CommitEdit();

        Assert.Equal(200.0 / 1600, scene.Placement.AnchorX, 6);
        Assert.Equal(300.0 / 900, scene.Placement.AnchorY, 6);
        Assert.False(scene.IsEditing);

        // And a cancel afterwards has nothing left to undo, which is what stops the cross
        // rolling the widget back to somewhere it was ten minutes ago.
        scene.CancelEdit();
        Assert.Equal(200.0 / 1600, scene.Placement.AnchorX, 6);
    }

    [Fact]
    public void EnteringEditModeTwiceDoesNotMoveTheGoalposts()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(100, 100));
        scene.BeginEdit();      // the tray asking for a mode that is already on
        scene.CancelEdit();

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);
    }

    [Fact]
    public void EditModeShowsTheSymbolWhateverGreenlightSays()
    {
        var scene = Sized();
        scene.ForceVisible = true;
        scene.SnapVisible();

        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void EditingARedOneShowsTheCloudRatherThanReplayingTheBomb()
    {
        var scene = Sized();
        scene.State = BlastState.Red;
        scene.SnapVisible();

        // Straight to the standing cloud. Entering edit mode is not news, and a detonation
        // going off because somebody wanted to nudge the widget would be a lie about the build.
        Assert.Equal(1, scene.Blast);
    }

    // ── the long grass ────────────────────────────────────────────────────────

    [Fact]
    public void AFrameFromAfterTheMachineWokeUpIsClamped()
    {
        var scene = Sized();
        scene.State = BlastState.Red;

        // The lid was shut for two minutes. Unclamped, this would run the whole detonation
        // inside one frame and the bomb would be over before it was drawn.
        scene.Advance(TimeSpan.FromMinutes(2));

        Assert.True(scene.Blast < 0.2, $"the detonation jumped to {scene.Blast:F2} in one frame");
    }

    [Fact]
    public void AZeroSizedOverlayDoesNotProduceNonsense()
    {
        var scene = Scene();
        scene.Resize(0, 0);

        var layout = scene.Measure();

        Assert.True(double.IsFinite(layout.Box.Width));
        Assert.True(layout.Box.Width >= FalloutScene.MinimumSide);
    }
}

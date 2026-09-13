using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.FalloutClient;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct AreaBounds(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>How much of the screen the symbol is allowed to stand on.</summary>
public enum AreaChoice
{
    /// <summary>
    /// The desktop as far as the taskbar. A symbol dragged to the bottom of the screen still
    /// leaves the Start button clickable, which on the primary monitor matters more than the
    /// last forty pixels.
    /// </summary>
    WorkArea,

    /// <summary>The whole screen, taskbar included.</summary>
    FullScreen,
}

/// <summary>
/// Works out the rectangle the overlay covers. Unlike the cars, the symbol is not attached
/// to anything — they stand wherever they were put — so this is only ever asked for the
/// bounds of the desktop.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DesktopArea
{
    private const uint SpiGetWorkArea = 0x0030;

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, ref Rect pvParam, uint fWinIni);

    /// <summary>Where the overlay goes, in physical pixels.</summary>
    public static AreaBounds Find(AreaChoice choice)
    {
        if (choice == AreaChoice.WorkArea)
        {
            var work = new Rect();
            if (SystemParametersInfoW(SpiGetWorkArea, 0, ref work, 0))
            {
                var width = work.Right - work.Left;
                var height = work.Bottom - work.Top;
                if (width > 0 && height > 0) return new AreaBounds(work.Left, work.Top, width, height);
            }
        }

        // The virtual screen rather than the primary one, so the overlay reaches across a
        // second monitor instead of cutting the symbol off at the edge of the first.
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var cx = GetSystemMetrics(SmCxVirtualScreen);
        var cy = GetSystemMetrics(SmCyVirtualScreen);

        return cx > 0 && cy > 0 ? new AreaBounds(x, y, cx, cy) : new AreaBounds(0, 0, 1280, 720);
    }
}

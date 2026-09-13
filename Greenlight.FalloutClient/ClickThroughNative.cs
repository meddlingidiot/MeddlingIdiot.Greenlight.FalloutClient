using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.FalloutClient;

/// <summary>
/// Makes the overlay furniture rather than an application: the mouse falls straight through
/// it, it never takes focus, and it stays out of Alt+Tab — until edit mode asks for it
/// back.
/// </summary>
/// <remarks>
/// The one that is easy to leave out is <c>WS_EX_LAYERED</c>: Avalonia's windows are
/// composited, and on a composited window <c>WS_EX_TRANSPARENT</c> alone does not take it out
/// of hit testing — clicks land on the symbol instead of on the desktop underneath.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class ClickThroughNative
{
    private const int GwlExStyle = -20;

    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;

    private const uint LwaAlpha = 0x00000002;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    public static void Apply(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        Set(hwnd, WsExTransparent | WsExNoActivate | WsExToolWindow | WsExLayered, on: true);

        // A window that has just become layered needs its alpha stated once. 255 means "no
        // uniform fade"; the symbol's own translucency comes from how it is drawn.
        SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
    }

    /// <summary>
    /// Let the mouse hit the window, or stop it. Edit mode is the only thing that turns
    /// this on: the rest of the time the overlay covers the entire desktop, and a desktop you
    /// cannot click is not a desk toy, it is a fault.
    /// </summary>
    /// <remarks>
    /// <c>WS_EX_TOOLWINDOW</c> and <c>WS_EX_LAYERED</c> stay put through both. Dropping the
    /// tool-window bit would put the overlay in Alt+Tab for as long as somebody was dragging it,
    /// and dropping layered would take the composited window out of hit testing again — which
    /// is the exact opposite of what this is for.
    /// </remarks>
    public static void SetInteractive(IntPtr hwnd, bool interactive)
    {
        if (hwnd == IntPtr.Zero) return;

        Set(hwnd, WsExTransparent | WsExNoActivate, on: !interactive);

        // Windows caches the extended styles for hit testing; without the frame-changed nudge
        // the change can take until the next move or resize to be believed, which from the
        // mouse's point of view is "edit mode did not turn on".
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    /// <summary>
    /// Put the window back at the top of the z-order. Worth doing on a timer rather than
    /// once.
    /// </summary>
    /// <remarks>
    /// "Topmost" is not a rank, it is a band, and the shell is in the same band — so the last
    /// window to claim it wins, and the shell claims it often. Setting the flag once at
    /// startup gets you a symbol that is on top until the first time somebody clicks the
    /// taskbar and then silently behind everything forever, which looks exactly like the app
    /// having crashed.
    /// </remarks>
    public static void KeepOnTop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private static void Set(IntPtr hwnd, int bits, bool on)
    {
        // Widened through uint first: style bits are a bit pattern, not a number, and a
        // 32-bit signed value with the top bit set would sign-extend into the 64-bit style
        // word and turn on every high bit — WS_EX_NOACTIVATE among them.
        var mask = (long)(uint)bits;

        var current = (long)GetWindowLongPtrW(hwnd, GwlExStyle);
        var updated = on ? current | mask : current & ~mask;
        if (updated != current) SetWindowLongPtrW(hwnd, GwlExStyle, (IntPtr)updated);
    }
}

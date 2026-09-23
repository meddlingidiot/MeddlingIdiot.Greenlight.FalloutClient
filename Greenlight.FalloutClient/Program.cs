using Automation.Velopack;
using Avalonia;
using Velopack;

namespace Greenlight.FalloutClient;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Before Avalonia, and before anything else. Velopack's hooks — first run, update,
        // uninstall — are handled inside Run(), which then exits the process; started any later
        // and an install would flash a window on its way past, or miss the hook altogether.
        VelopackApp.Build().Run();
        VelopackBootstrapper.Startup("Greenlight.FalloutClient", args);

        // One per session, and the second one leaves without a word. Two copies — the one
        // Windows started and the one somebody launched by hand — would each react to the same
        // build and fight over the screen, which looks like a bug in the client rather than
        // like two of it.
        //
        // Taken after the Velopack hooks, so an install or an update running beside a copy that
        // is already up is never turned away. Local\ is this logon session, so two people signed
        // in to the same machine each get their own.
        using var single = new Mutex(initiallyOwned: true, @"Local\Greenlight.FalloutClient", out var first);
        if (!first) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Greenlight.FalloutClient;

/// <summary>
/// Whether the symbol comes back when Windows does. One value under the current user's
/// <c>Run</c> key — no scheduled task, no service, nothing needing an administrator.
/// </summary>
/// <remarks>
/// <para>
/// The registry is the only record of this. There is deliberately no matching setting in
/// <c>fallout.json</c>: the user can turn the same thing off in Task Manager's Startup tab or
/// in Settings, and a copy of the answer in a file of ours would be wrong the moment they did
/// — with no way to tell which of the two was lying.
/// </para>
/// <para>
/// Nothing here throws. A locked-down machine with a policy on the Run key is a perfectly
/// ordinary thing to meet, and a desk toy that fell over because it could not offer to start
/// itself would be worse than one that quietly cannot.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// The name of the value under <c>Run</c>. This is what the user sees in Task Manager's
    /// Startup tab, so it is a name rather than a path.
    /// </summary>
    private const string ValueName = "Greenlight Fallout";

    /// <summary>Whether Windows is currently set to start the symbol.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ask Windows to start the symbol, or stop asking. Returns what it actually is afterwards.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return IsEnabled();

            if (enabled)
            {
                var command = Command();
                if (command is null) return false;

                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return IsEnabled();
        }
        catch
        {
            return IsEnabled();
        }
    }

    /// <summary>
    /// Put the registered path back in step with where this app now lives, if it is registered
    /// at all.
    /// </summary>
    /// <remarks>
    /// For the case the launcher below cannot rule out: somebody who turned this on under an
    /// older build that registered the versioned path, or who moved a portable copy. Called at
    /// startup, when this process is by definition the one that should be starting.
    /// </remarks>
    public static void Refresh()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not string current) return;

            var command = Command();
            if (command is null || string.Equals(current, command, StringComparison.OrdinalIgnoreCase)) return;

            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        catch
        {
            // Same as everywhere else here: not being able to tidy the registry is not worth
            // interrupting anybody over.
        }
    }

    private static string? Command()
    {
        var launcher = Launcher(Environment.ProcessPath, File.Exists);

        // Quoted, because the path contains the user's name and Program Files contains a
        // space. An unquoted Run value with a space in it is run as its first word plus
        // arguments, which fails silently at every login and nowhere else.
        return launcher is null ? null : $"\"{launcher}\"";
    }

    /// <summary>
    /// The executable Windows should be pointed at: the stable shim beside the install, not
    /// the versioned copy this process is running from.
    /// </summary>
    /// <remarks>
    /// A Velopack install looks like <c>…\Greenlight.FalloutClient\current\Greenlight.FalloutClient.exe</c>,
    /// with a shim of the same name one level up that always launches whatever
    /// <c>current</c> points at. Registering the running path instead would work perfectly
    /// until the first update replaced <c>current</c>, and then fail at every login
    /// afterwards — the kind of fault nobody connects to a setting they turned on weeks
    /// earlier. Anything that is not that layout — a dev build out of <c>bin</c>, a portable
    /// copy — is registered as it stands.
    /// </remarks>
    /// <param name="processPath">Where this process is running from.</param>
    /// <param name="exists">How to check for the shim. A parameter so this can be tested.</param>
    public static string? Launcher(string? processPath, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(processPath)) return null;

        var directory = Path.GetDirectoryName(processPath);
        if (directory is null) return processPath;

        if (!string.Equals(Path.GetFileName(directory), "current", StringComparison.OrdinalIgnoreCase))
            return processPath;

        var root = Path.GetDirectoryName(directory);
        if (root is null) return processPath;

        var shim = Path.Combine(root, Path.GetFileName(processPath));

        // Only if it is actually there. A folder that happens to be called "current" is not a
        // promise, and pointing Windows at an executable that does not exist would be a worse
        // outcome than pointing it at a versioned one.
        return exists(shim) ? shim : processPath;
    }
}

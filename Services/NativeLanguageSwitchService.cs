using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Hides Windows' own centered language-switch popup (the duplicate of our
/// overlay) by toggling the undocumented
/// <c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\ImmersiveShell\UseNewLanguageSwitch</c>
/// value: 0 = old switch (no centered popup), 1 = modern popup.
///
/// Writing HKLM requires administrator rights, so <see cref="Apply"/> relaunches
/// this same executable elevated (one UAC prompt). The elevated instance is
/// recognised in <c>App.OnStartup</c> by <see cref="CliArg"/>, writes the value
/// via <see cref="WriteRegistry"/> and exits without starting the tray app.
/// </summary>
public static class NativeLanguageSwitchService
{
    private const string KeyPath   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\ImmersiveShell";
    private const string ValueName = "UseNewLanguageSwitch";

    /// <summary>CLI flag for the elevated helper, followed by the value to write (0 or 1).</summary>
    public const string CliArg = "--set-langswitch";

    /// <summary>True when the native popup is currently suppressed (value == 0).</summary>
    public static bool IsSuppressed()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: false);
            return key?.GetValue(ValueName) is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Requests the change with a single UAC prompt by relaunching elevated.
    /// Returns true if the helper applied it, false if the user declined UAC
    /// or the write failed.
    /// </summary>
    public static bool Apply(bool suppress)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        var psi = new ProcessStartInfo
        {
            FileName        = exe,
            Arguments       = $"{CliArg} {(suppress ? 0 : 1)}",
            UseShellExecute = true,   // required for the "runas" verb
            Verb            = "runas", // triggers UAC elevation
            CreateNoWindow  = true,
        };

        try
        {
            var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // ERROR_CANCELLED (1223): user dismissed the UAC dialog.
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(Apply));
            return false;
        }
    }

    /// <summary>
    /// Runs inside the elevated helper instance. Writes the HKLM value and
    /// returns a process exit code (0 = success).
    /// </summary>
    public static int WriteRegistry(bool suppress)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(KeyPath, writable: true);
            key.SetValue(ValueName, suppress ? 0 : 1, RegistryValueKind.DWord);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(WriteRegistry));
            return 1;
        }
    }

    /// <summary>
    /// Restarts Explorer so the change takes effect without a full sign-out.
    /// Runs unelevated — a user may restart their own shell.
    /// </summary>
    public static void RestartExplorer()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { /* ignore */ }
            }
            // Explorer usually auto-restarts; start it explicitly to be safe.
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(RestartExplorer));
        }
    }
}

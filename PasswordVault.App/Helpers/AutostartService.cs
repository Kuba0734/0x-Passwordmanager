using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace PasswordVault.App.Helpers;

public enum AutostartState { Enabled, Disabled, DisabledByUser, DisabledByPolicy, Unavailable }

/// <summary>
/// "Mit Windows starten": als MSIX über den StartupTask aus dem Paketmanifest,
/// unpaketiert (Entwicklung) über den Run-Schlüssel in der Registry.
/// </summary>
public static class AutostartService
{
    public const string TaskId = "PasswordVaultStartup";
    public const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "PasswordVault";

    private static readonly Lazy<bool> Packaged = new(() =>
    {
        try { return Package.Current != null; }
        catch { return false; }
    });

    public static bool IsPackaged => Packaged.Value;

    public static async Task<AutostartState> GetStateAsync()
    {
        try
        {
            if (!IsPackaged)
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(RunValue) != null ? AutostartState.Enabled : AutostartState.Disabled;
            }

            var task = await StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch
        {
            return AutostartState.Unavailable;
        }
    }

    /// <summary>Schaltet den Autostart um und liefert den tatsächlichen neuen Zustand.</summary>
    public static async Task<AutostartState> SetAsync(bool enable)
    {
        try
        {
            if (!IsPackaged)
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enable)
                    key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" {TrayArgument}");
                else
                    key.DeleteValue(RunValue, throwOnMissingValue: false);
                return await GetStateAsync();
            }

            var task = await StartupTask.GetAsync(TaskId);
            if (enable)
                return Map(await task.RequestEnableAsync());
            task.Disable();
            return Map(task.State);
        }
        catch
        {
            return AutostartState.Unavailable;
        }
    }

    public static string Describe(AutostartState state) => state switch
    {
        AutostartState.Enabled => "Startet bei der Anmeldung unsichtbar im Infobereich.",
        AutostartState.DisabledByUser => "Im Task-Manager unter „Autostart“ deaktiviert – dort wieder aktivieren.",
        AutostartState.DisabledByPolicy => "Durch eine Richtlinie deaktiviert.",
        AutostartState.Unavailable => "Auf diesem System nicht verfügbar.",
        _ => "Startet bei der Anmeldung unsichtbar im Infobereich, damit die Browser-Erweiterung sofort verbunden ist."
    };

    private static AutostartState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => AutostartState.Enabled,
        StartupTaskState.DisabledByUser => AutostartState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => AutostartState.DisabledByPolicy,
        _ => AutostartState.Disabled
    };
}

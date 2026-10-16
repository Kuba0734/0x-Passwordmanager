using Microsoft.UI.Xaml;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Services;
using System;
using System.IO;

namespace PasswordVault.App;

public partial class App : Application
{
    // Globale Services
    public static VaultStore Store { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;
    public static BrowserBridgeService Bridge { get; private set; } = null!;
    public static string DataDirectory { get; private set; } = "";
    public static string VaultPath => Store.FilePath;

    private Window? m_window;

    public App()
    {
        InitializeComponent();

        DataDirectory = ResolveDataDirectory();
        Store = new VaultStore(Path.Combine(DataDirectory, "vault.dat"));
        Settings = new SettingsService(DataDirectory);
        Bridge = new BrowserBridgeService(Store, Settings);

        UnhandledException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Unhandled] {e.Exception}");
            e.Handled = true;
            m_window?.DispatcherQueue.TryEnqueue(() =>
                MainWindowInstance.ShowToast("Unerwarteter Fehler: " + e.Exception.Message, ""));
        };
    }

    private static string ResolveDataDirectory()
    {
        // Entwickler-Override (z.B. für Tests, ohne echten Tresor anzufassen)
        var overrideDir = Environment.GetEnvironmentVariable("PASSWORDVAULT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            Directory.CreateDirectory(overrideDir);
            return overrideDir;
        }

        try
        {
            // Packaged App: Windows-verwalteter Pfad
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            // Fallback für Unpackaged/Debug
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PasswordVault");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        m_window = window;

        // Autostart: unsichtbar im Infobereich starten, nur die Browser-Verbindung läuft
        if (WasStartedByAutostart())
            window.StartInTray();
        else
            window.Activate();

        if (Settings.Current.BrowserBridgeEnabled)
            Bridge.Start();
    }

    private static bool WasStartedByAutostart()
    {
        try
        {
            var kind = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind;
            if (kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask) return true;
        }
        catch { }
        return Array.Exists(Environment.GetCommandLineArgs(),
            a => a.Equals(AutostartService.TrayArgument, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Jemand hat die App ein zweites Mal gestartet → vorhandenes Fenster zeigen.</summary>
    public static void OnSecondInstanceActivated()
    {
        if (Current is App { m_window: MainWindow window })
            window.DispatcherQueue.TryEnqueue(window.ShowFromTray);
    }

    public static MainWindow MainWindowInstance
        => (MainWindow)((App)Current).m_window!;
}

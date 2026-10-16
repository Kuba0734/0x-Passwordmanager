using Microsoft.UI.Xaml;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace PasswordVault.App;

public partial class App : Application
{
    // Globale Services
    public static VaultService Vault { get; } = new();
    public static VaultFileService VaultFile { get; } = new();
    public static string VaultPath { get; private set; } = "";
    public static SettingsService Settings { get; private set; } = null!;
    private static readonly BrowserBridgeService _bridge = new();

    public static List<PasswordVault.Core.Models.PasswordEntry>? CurrentEntries { get; set; }


    private Window? m_window;

    public App()
    {
        InitializeComponent();

        // Für Packaged Apps den richtigen Pfad nutzen
        string vaultDir;
        try
        {
            // Packaged App: nutze den Windows-verwalteten Pfad
            vaultDir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            // Fallback für Unpackaged/Debug
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            vaultDir = Path.Combine(appData, "PasswordVault");
            Directory.CreateDirectory(vaultDir);
        }

        VaultPath = Path.Combine(vaultDir, "vault.dat");
        Settings = new SettingsService(vaultDir);
    }

    protected override void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        _bridge.Start();
        m_window = new MainWindow();
        m_window.Activate();
    }

    public static MainWindow MainWindowInstance
        => (MainWindow)((App)Current).m_window!;
}
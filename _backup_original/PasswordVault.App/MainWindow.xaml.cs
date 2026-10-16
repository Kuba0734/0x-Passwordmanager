using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PasswordVault.App.Helpers;
using System.Collections.Generic;

namespace PasswordVault.App;

public sealed partial class MainWindow : Window
{
    private InactivityService? _inactivity;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        SystemBackdrop = new MicaBackdrop
        {
            Kind = MicaKind.Base
        };

        var appWindow = this.AppWindow;
        appWindow.Resize(
            new Windows.Graphics.SizeInt32(1100, 700));
        appWindow.Title = "Password Vault";

        // Auto-Lock aus Settings
        var lockSeconds = App.Settings.Current.AutoLockSeconds;
        if (lockSeconds > 0)
        {
            _inactivity = new InactivityService(
                DispatcherQueue, lockSeconds);
            _inactivity.OnTimeout += () =>
            {
                App.Vault.Lock();
                NavigateToUnlock();
            };
        }

        // Benutzeraktivität tracken
        RootFrame.PointerMoved += (s, e) =>
            _inactivity?.ResetTimer();
        RootFrame.KeyDown += (s, e) =>
            _inactivity?.ResetTimer();

        RootFrame.Navigate(typeof(Views.UnlockPage));

        // Gespeichertes Theme anwenden
        var theme = App.Settings.Current.Theme;
        if (RootFrame.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

    }

    public void NavigateToMain()
    {
        RootFrame.Navigate(typeof(Views.MainPage));
        _inactivity?.ResetTimer();
    }

    public void NavigateToUnlock()
    {
        RootFrame.Navigate(typeof(Views.UnlockPage));
        _inactivity?.Stop();
    }

    public void RefreshEntriesFromBridge(List<PasswordVault.Core.Models.PasswordEntry> entries)
    {
        App.CurrentEntries = entries;
        RootFrame.Navigate(typeof(Views.MainPage));
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using System;
using System.Collections.Generic;

namespace PasswordVault.App.Views;

public sealed partial class UnlockPage : Page
{
    public UnlockPage()
    {
        InitializeComponent();
        if (App.VaultFile.Exists(App.VaultPath))
        {
            UnlockButton.Content = "Entsperren";
            ShowWindowsHelloIfEnabled();
        }
        else
        {
            UnlockButton.Content = "Erstellen";
        }
    }

    private void ShowWindowsHelloIfEnabled()
    {
        if (App.Settings.Current.WindowsHelloEnabled
            && WindowsHelloService.HasStoredPassword())
        {
            WindowsHelloButton.Visibility = Visibility.Visible;
        }
    }

    private async void WindowsHelloButton_Click(
        object sender, RoutedEventArgs e)
    {
        var authenticated =
            await WindowsHelloService.AuthenticateAsync();
        if (!authenticated)
        {
            ShowError("Windows PIN-Authentifizierung fehlgeschlagen.");
            return;
        }

        var storedPassword =
            WindowsHelloService.RetrievePassword();
        if (string.IsNullOrEmpty(storedPassword))
        {
            ShowError("Kein gespeichertes Passwort gefunden. Bitte melde dich mit dem Master-Passwort an.");
            return;
        }

        try
        {
            var (metadata, encrypted) =
                App.VaultFile.Load(App.VaultPath);
            App.Vault.Unlock(storedPassword, metadata);
            var entries = App.Vault.DecryptEntries(encrypted);
            App.CurrentEntries = entries;
            Frame.Navigate(typeof(MainPage));
        }
        catch (Exception ex)
        {
            if (ex is System.Security.Cryptography
                .AuthenticationTagMismatchException)
            {
                // Gespeichertes Passwort stimmt nicht mehr
                // (z.B. nach Passwort-Änderung)
                WindowsHelloService.RemoveStoredPassword();
                App.Settings.Current.WindowsHelloEnabled = false;
                App.Settings.Save();
                WindowsHelloButton.Visibility = Visibility.Collapsed;
                ShowError("Das gespeicherte Passwort ist ungültig. Windows PIN wurde deaktiviert. Bitte melde dich mit dem Master-Passwort an.");
            }
            else
            {
                ShowError($"Fehler: {ex.Message}");
            }
        }
    }

    private void PasswordBox_KeyDown(
        object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            UnlockButton_Click(sender, new RoutedEventArgs());
    }

    private void UnlockButton_Click(
        object sender, RoutedEventArgs e)
    {
        var password = MasterPasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Bitte gib ein Passwort ein.");
            return;
        }

        try
        {

            if (App.VaultFile.Exists(App.VaultPath))
            {
                var (metadata, encrypted) =
                    App.VaultFile.Load(App.VaultPath);
                App.Vault.Unlock(password, metadata);
                var entries = App.Vault.DecryptEntries(encrypted);
                App.CurrentEntries = entries;
                Frame.Navigate(typeof(MainPage));
            }
            else
            {
                var metadata = App.Vault.CreateVault(password);
                var entries = new List<PasswordVault.Core.Models.PasswordEntry>();
                var encrypted = App.Vault.EncryptEntries(entries);
                App.VaultFile.Save(App.VaultPath, metadata, encrypted);
                App.CurrentEntries = entries;
                Frame.Navigate(typeof(MainPage));
            }
        }
        catch (Exception ex)
        {
            if (ex is System.Security.Cryptography.AuthenticationTagMismatchException)
            {
                ShowError("Falsches Passwort. Bitte versuche es erneut.");
            }
            else
            {
                ShowError($"Fehler: {ex.Message}");
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }
}

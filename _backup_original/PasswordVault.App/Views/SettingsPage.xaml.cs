using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordVault.App.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordVault.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isLoadingSettings;

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        LoadWindowsHelloAsync();
    }

    private async void LoadWindowsHelloAsync()
    {
        var available = await WindowsHelloService.IsAvailableAsync();
        if (available)
        {
            WindowsHelloCard.Visibility = Visibility.Visible;
            _isLoadingSettings = true;
            WindowsHelloToggle.IsOn =
                App.Settings.Current.WindowsHelloEnabled
                && WindowsHelloService.HasStoredPassword();
            _isLoadingSettings = false;
        }
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        var s = App.Settings.Current;

        // Auto-Lock Combo setzen
        SelectComboByTag(AutoLockCombo,
            s.AutoLockSeconds.ToString());
        AutoLockDescription.Text =
            s.AutoLockSeconds == 0 ? "Deaktiviert"
            : $"Nach {s.AutoLockSeconds / 60} Minuten";

        // Clipboard Combo setzen
        SelectComboByTag(ClipboardCombo,
            s.ClipboardClearSeconds.ToString());
        ClipboardDescription.Text =
            $"Nach {s.ClipboardClearSeconds} Sekunden";

        // Theme setzen
        foreach (RadioButton rb in
            ThemeRadio.Items.Cast<RadioButton>())
        {
            if (rb.Tag?.ToString() == s.Theme)
                rb.IsChecked = true;
        }

        // Vault-Pfad anzeigen
        VaultPathText.Text = App.VaultPath;
        _isLoadingSettings = false;
    }

    private void AutoLock_Changed(
        object sender, SelectionChangedEventArgs e)
    {
        if (AutoLockCombo.SelectedItem
            is not ComboBoxItem item) return;
        var seconds = int.Parse(
            item.Tag?.ToString() ?? "300");
        App.Settings.Current.AutoLockSeconds = seconds;
        App.Settings.Save();
        AutoLockDescription.Text =
            seconds == 0 ? "Deaktiviert"
            : $"Nach {seconds / 60} Minuten";
    }

    private void Clipboard_Changed(
        object sender, SelectionChangedEventArgs e)
    {
        if (ClipboardCombo.SelectedItem
            is not ComboBoxItem item) return;
        var seconds = int.Parse(
            item.Tag?.ToString() ?? "15");
        App.Settings.Current.ClipboardClearSeconds =
            seconds;
        App.Settings.Save();
        ClipboardDescription.Text =
            $"Nach {seconds} Sekunden";
    }

    private void Theme_Changed(
        object sender, SelectionChangedEventArgs e)
    {
        if (ThemeRadio.SelectedItem
            is not RadioButton rb) return;
        var theme = rb.Tag?.ToString() ?? "System";
        App.Settings.Current.Theme = theme;
        App.Settings.Save();

        // Theme sofort anwenden
        if (this.XamlRoot?.Content
            is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private async void ChangeMasterPassword_Click(
        object sender, RoutedEventArgs e)
    {
        var newPwBox = new PasswordBox
        {
            PlaceholderText = "Neues Master-Passwort",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var confirmBox = new PasswordBox
        {
            PlaceholderText = "Passwort bestätigen"
        };

        var panel = new StackPanel();
        panel.Children.Add(newPwBox);
        panel.Children.Add(confirmBox);

        var dialog = new ContentDialog
        {
            Title = "Master-Passwort ändern",
            Content = panel,
            PrimaryButtonText = "Ändern",
            CloseButtonText = "Abbrechen",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        if (newPwBox.Password != confirmBox.Password)
        {
            await new ContentDialog
            {
                Title = "Fehler",
                Content = "Passwörter stimmen nicht überein.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        if (newPwBox.Password.Length < 8)
        {
            await new ContentDialog
            {
                Title = "Fehler",
                Content = "Mindestens 8 Zeichen erforderlich.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        try
        {
            var (_, currentEncrypted) =
                App.VaultFile.Load(App.VaultPath);
            var (newMeta, newEnc) =
                App.Vault.ChangePassword(
                    newPwBox.Password, currentEncrypted);
            App.VaultFile.Save(
                App.VaultPath, newMeta, newEnc);

            // Gespeichertes Windows Hello Passwort aktualisieren
            if (App.Settings.Current.WindowsHelloEnabled
                && WindowsHelloService.HasStoredPassword())
            {
                WindowsHelloService.StorePassword(
                    newPwBox.Password);
            }

            await new ContentDialog
            {
                Title = "Erfolg",
                Content = "Master-Passwort wurde geändert.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Fehler",
                Content = $"Fehler: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }


    private async void WindowsHello_Toggled(
        object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        if (WindowsHelloToggle.IsOn)
        {
            // Benutzer muss aktuelles Master-Passwort bestätigen
            var pwBox = new PasswordBox
            {
                PlaceholderText = "Aktuelles Master-Passwort"
            };

            var dialog = new ContentDialog
            {
                Title = "Windows PIN aktivieren",
                Content = pwBox,
                PrimaryButtonText = "Aktivieren",
                CloseButtonText = "Abbrechen",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary
                || string.IsNullOrEmpty(pwBox.Password))
            {
                _isLoadingSettings = true;
                WindowsHelloToggle.IsOn = false;
                _isLoadingSettings = false;
                return;
            }

            // Passwort verifizieren
            try
            {
                var (metadata, encrypted) =
                    App.VaultFile.Load(App.VaultPath);
                var testVault = new PasswordVault.Core.Services.VaultService();
                testVault.Unlock(pwBox.Password, metadata);
                testVault.DecryptEntries(encrypted);
                testVault.Lock();
            }
            catch
            {
                _isLoadingSettings = true;
                WindowsHelloToggle.IsOn = false;
                _isLoadingSettings = false;

                await new ContentDialog
                {
                    Title = "Fehler",
                    Content = "Falsches Passwort.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                return;
            }

            // Windows Hello Authentifizierung
            var authenticated =
                await WindowsHelloService.AuthenticateAsync();
            if (!authenticated)
            {
                _isLoadingSettings = true;
                WindowsHelloToggle.IsOn = false;
                _isLoadingSettings = false;
                return;
            }

            // Passwort im Credential Locker speichern
            WindowsHelloService.StorePassword(pwBox.Password);
            App.Settings.Current.WindowsHelloEnabled = true;
            App.Settings.Save();
        }
        else
        {
            // Deaktivieren: Gespeichertes Passwort entfernen
            WindowsHelloService.RemoveStoredPassword();
            App.Settings.Current.WindowsHelloEnabled = false;
            App.Settings.Save();
        }
    }

    private async void ExportCsv_Click(
        object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers
            .FileSavePicker();

        // WinUI 3 braucht das Window-Handle
        var hwnd = WinRT.Interop.WindowNative
            .GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow
            .Initialize(picker, hwnd);

        picker.SuggestedFileName = "passwords_export";
        picker.FileTypeChoices.Add(
            "CSV-Datei", new List<string> { ".csv" });

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var (_, encrypted) =
                App.VaultFile.Load(App.VaultPath);
            var entries =
                App.Vault.DecryptEntries(encrypted);
            var csv = new PasswordVault.Core.Services
                .CsvService().Export(entries);

            await Windows.Storage.FileIO
                .WriteTextAsync(file, csv);

            await new ContentDialog
            {
                Title = "Export erfolgreich",
                Content = $"{entries.Count} Einträge exportiert.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Export fehlgeschlagen",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }


    private async void ImportCsv_Click(
        object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers
            .FileOpenPicker();

        var hwnd = WinRT.Interop.WindowNative
            .GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow
            .Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".csv");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var csvText = await Windows.Storage.FileIO
                .ReadTextAsync(file);
            var csvService = new PasswordVault.Core
                .Services.CsvService();
            var imported = csvService.Import(csvText);

            if (imported.Count == 0)
            {
                await new ContentDialog
                {
                    Title = "Keine Einträge gefunden",
                    Content = "Die CSV-Datei enthält keine erkennbaren Einträge.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                return;
            }

            // Bestätigung
            var confirm = await new ContentDialog
            {
                Title = "Import bestätigen",
                Content = $"{imported.Count} Einträge gefunden. Importieren?",
                PrimaryButtonText = "Importieren",
                CloseButtonText = "Abbrechen",
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (confirm != ContentDialogResult.Primary)
                return;

            // Bestehende Einträge laden + neue hinzufügen
            var (meta, encrypted) =
                App.VaultFile.Load(App.VaultPath);
            var existing =
                App.Vault.DecryptEntries(encrypted);
            existing.AddRange(imported);

            // Neu verschlüsseln und speichern
            var newEncrypted =
                App.Vault.EncryptEntries(existing);
            App.VaultFile.Save(
                App.VaultPath, meta, newEncrypted);

            await new ContentDialog
            {
                Title = "Import erfolgreich",
                Content = $"{imported.Count} Einträge importiert.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();

        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Import fehlgeschlagen",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }

    private void CopyVaultPath_Click(
    object sender, RoutedEventArgs e)
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(App.VaultPath);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }


    private static void SelectComboByTag(
        ComboBox combo, string tag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item
                && item.Tag?.ToString() == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Services;
using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PasswordVault.App.Views;

public sealed partial class UnlockPage : Page
{
    private readonly bool _createMode;
    private bool _busy;

    public UnlockPage()
    {
        InitializeComponent();
        _createMode = !App.Store.Exists;

        if (_createMode)
        {
            TitleText.Text = "Tresor erstellen";
            SubtitleText.Text = "Lege ein starkes Master-Passwort fest. Es schützt alle deine Zugangsdaten.";
            UnlockButtonText.Text = "Tresor erstellen";
            CreatePanel.Visibility = Visibility.Visible;
            MasterPasswordBox.PlaceholderText = "Neues Master-Passwort";
            UpdateStrength();
        }
        else if (App.Settings.Current.WindowsHelloEnabled
                 && WindowsHelloService.HasStoredPassword())
        {
            HelloPanel.Visibility = Visibility.Visible;
        }

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hintergrund
        Motion.Float(BlobA, new Vector3(90, 60, 0), 14000);
        Motion.Float(BlobB, new Vector3(-110, -70, 0), 17000, 800);
        Motion.Float(BlobC, new Vector3(-60, 90, 0), 12000, 400);
        Motion.Float(BlobD, new Vector3(70, -50, 0), 15000, 1200);

        // Karte + Inhalt gestaffelt
        Motion.FadeInUp(Card, 0, 40, 700);
        Motion.Stagger(Form, 60, 150);
        Motion.PopIn(LogoTile, 120, 0.4f);
        Motion.Ripple(Ring1, 2600, 600);
        Motion.Ripple(Ring2, 2600, 1900);

        MasterPasswordBox.Focus(FocusState.Programmatic);
    }

    private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        if (_createMode && ReferenceEquals(sender, MasterPasswordBox))
            ConfirmPasswordBox.Focus(FocusState.Keyboard);
        else
            UnlockButton_Click(sender, new RoutedEventArgs());
    }

    private void MasterPassword_Changed(object sender, RoutedEventArgs e)
    {
        // Fehler erst ausblenden, wenn der Benutzer wieder tippt (nicht beim programmatischen Leeren)
        if (ErrorPanel.Visibility == Visibility.Visible && MasterPasswordBox.Password.Length > 0)
            ErrorPanel.Visibility = Visibility.Collapsed;
        if (_createMode) UpdateStrength();
    }

    private void UpdateStrength()
    {
        var result = PasswordStrength.Evaluate(MasterPasswordBox.Password);
        StrengthFill.Background = Ui.StrengthBrush(result.Level);
        StrengthLabel.Text = result.Level == StrengthLevel.None ? "—" : result.Label;
        Motion.ScaleXTo(StrengthFill, Math.Max(result.Score, 2) / 100f, 450);
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var password = MasterPasswordBox.Password;

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Bitte gib dein Master-Passwort ein.");
            return;
        }

        if (_createMode)
        {
            if (password.Length < 8)
            {
                ShowError("Das Master-Passwort muss mindestens 8 Zeichen lang sein.");
                return;
            }
            if (password != ConfirmPasswordBox.Password)
            {
                ShowError("Die Passwörter stimmen nicht überein.");
                ConfirmPasswordBox.Password = "";
                ConfirmPasswordBox.Focus(FocusState.Programmatic);
                return;
            }
        }

        SetBusy(true);
        try
        {
            // Argon2 braucht spürbar Zeit – nicht auf dem UI-Thread rechnen
            if (_createMode)
                await Task.Run(() => App.Store.Create(password));
            else
                await Task.Run(() => App.Store.Unlock(password));

            await PlaySuccessAsync();
            App.MainWindowInstance.NavigateToMain();
        }
        catch (CryptographicException)
        {
            SetBusy(false);
            ShowError("Falsches Master-Passwort. Bitte versuche es erneut.");
            MasterPasswordBox.Password = "";
            MasterPasswordBox.Focus(FocusState.Programmatic);
        }
        catch (Exception ex)
        {
            SetBusy(false);
            ShowError($"Tresor konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private async void WindowsHelloButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var authenticated = await WindowsHelloService.AuthenticateAsync();
        if (!authenticated)
        {
            ShowError("Windows Hello-Bestätigung abgebrochen oder fehlgeschlagen.");
            return;
        }

        var storedPassword = WindowsHelloService.RetrievePassword();
        if (string.IsNullOrEmpty(storedPassword))
        {
            ShowError("Kein gespeichertes Passwort gefunden. Bitte melde dich mit dem Master-Passwort an.");
            return;
        }

        SetBusy(true);
        try
        {
            await Task.Run(() => App.Store.Unlock(storedPassword));
            await PlaySuccessAsync();
            App.MainWindowInstance.NavigateToMain();
        }
        catch (CryptographicException)
        {
            // Gespeichertes Passwort ist veraltet (z.B. nach Passwort-Änderung)
            SetBusy(false);
            WindowsHelloService.RemoveStoredPassword();
            App.Settings.Current.WindowsHelloEnabled = false;
            App.Settings.Save();
            HelloPanel.Visibility = Visibility.Collapsed;
            ShowError("Das gespeicherte Passwort ist ungültig. Windows Hello wurde deaktiviert – bitte mit dem Master-Passwort anmelden.");
        }
        catch (Exception ex)
        {
            SetBusy(false);
            ShowError($"Fehler: {ex.Message}");
        }
    }

    private async Task PlaySuccessAsync()
    {
        LockIcon.Glyph = ""; // offenes Schloss
        Motion.Pulse(LogoTile, 1.18f);
        BusyRing.IsActive = false;
        BusyRing.Visibility = Visibility.Collapsed;
        UnlockButtonContent.Visibility = Visibility.Visible;
        UnlockButtonText.Text = _createMode ? "Tresor erstellt" : "Entsperrt";
        UnlockArrow.Glyph = "";
        Motion.PopIn(UnlockArrow, 0, 0.3f);
        if (Motion.Enabled) await Task.Delay(420);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        MasterPasswordBox.IsEnabled = !busy;
        ConfirmPasswordBox.IsEnabled = !busy;
        WindowsHelloButton.IsEnabled = !busy;
        BusyRing.IsActive = busy;
        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UnlockButtonContent.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        bool wasVisible = ErrorPanel.Visibility == Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Visible;
        if (!wasVisible) Motion.FadeInUp(ErrorPanel, 0, -8, 300);
        Motion.Shake(Card);
        Motion.Shake(LogoTile);
    }
}

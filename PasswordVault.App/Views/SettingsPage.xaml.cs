using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordVault.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        _loading = false;

        Loaded += async (_, _) =>
        {
            App.Bridge.StatusChanged += Bridge_StatusChanged;
            App.MainWindowInstance.AutostartChanged += OnAutostartChanged;
            Motion.Stagger(Root, 25);
            await App.MainWindowInstance.RefreshAutostartAsync();
            ShowAutostart(App.MainWindowInstance.AutostartState);
            await LoadWindowsHelloAsync();
        };
        Unloaded += (_, _) =>
        {
            App.Bridge.StatusChanged -= Bridge_StatusChanged;
            App.MainWindowInstance.AutostartChanged -= OnAutostartChanged;
        };
    }

    // ================= Start & Infobereich =================

    private void OnAutostartChanged()
        => DispatcherQueue.TryEnqueue(() => ShowAutostart(App.MainWindowInstance.AutostartState));

    private void ShowAutostart(AutostartState state)
    {
        _loading = true;
        AutostartToggle.IsOn = state == AutostartState.Enabled;
        AutostartToggle.IsEnabled = state is AutostartState.Enabled or AutostartState.Disabled;
        AutostartCard.Description = AutostartService.Describe(state);
        _loading = false;
    }

    private async void Autostart_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var wanted = AutostartToggle.IsOn;
        var state = await App.MainWindowInstance.SetAutostartAsync(wanted);
        ShowAutostart(state);
        if (wanted && state != AutostartState.Enabled)
            App.MainWindowInstance.ShowToast("Autostart konnte nicht aktiviert werden", "");
        else
            App.MainWindowInstance.ShowToast(wanted ? "Startet ab jetzt mit Windows" : "Autostart deaktiviert", "");
    }

    private void MinimizeToTray_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Current.MinimizeToTray = MinimizeToTrayToggle.IsOn;
        App.Settings.Save();
    }

    private void Bridge_StatusChanged(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(UpdateBridgeStatus);

    private void LoadSettings()
    {
        var s = App.Settings.Current;
        SelectByTag(AutoLockCombo, s.AutoLockSeconds.ToString());
        SelectByTag(ClipboardCombo, s.ClipboardClearSeconds.ToString());
        SelectByTag(ThemeCombo, s.Theme);
        AnimationsToggle.IsOn = s.AnimationsEnabled;
        BridgeToggle.IsOn = s.BrowserBridgeEnabled;
        MinimizeToTrayToggle.IsOn = s.MinimizeToTray;

        LengthSlider.Value = s.DefaultPasswordLength;
        LengthValue.Text = s.DefaultPasswordLength.ToString();
        UpperToggle.IsOn = s.UseUppercase;
        LowerToggle.IsOn = s.UseLowercase;
        DigitToggle.IsOn = s.UseDigits;
        SymbolToggle.IsOn = s.UseSymbols;
        AmbiguousToggle.IsOn = s.ExcludeAmbiguous;
        UpdateGeneratorSummary();

        VaultPathText.Text = App.VaultPath;
        UpdateBridgeStatus();
    }

    private async Task LoadWindowsHelloAsync()
    {
        if (!await WindowsHelloService.IsAvailableAsync()) return;
        WindowsHelloCard.Visibility = Visibility.Visible;
        _loading = true;
        WindowsHelloToggle.IsOn = App.Settings.Current.WindowsHelloEnabled
                                  && WindowsHelloService.HasStoredPassword();
        _loading = false;
    }

    // ================= Sicherheit =================

    private void AutoLock_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AutoLockCombo.SelectedItem is not ComboBoxItem item) return;
        App.Settings.Current.AutoLockSeconds = int.Parse((string)item.Tag);
        App.Settings.Save();
        App.MainWindowInstance.ApplyAutoLockSettings();
    }

    private void Clipboard_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ClipboardCombo.SelectedItem is not ComboBoxItem item) return;
        App.Settings.Current.ClipboardClearSeconds = int.Parse((string)item.Tag);
        App.Settings.Save();
    }

    private async void ChangeMasterPassword_Click(object sender, RoutedEventArgs e)
    {
        var currentBox = new PasswordBox { Header = "Aktuelles Master-Passwort", PasswordRevealMode = PasswordRevealMode.Peek };
        var newBox = new PasswordBox { Header = "Neues Master-Passwort", PasswordRevealMode = PasswordRevealMode.Peek };
        var confirmBox = new PasswordBox { Header = "Neues Passwort wiederholen", PasswordRevealMode = PasswordRevealMode.Peek };
        var strength = new TextBlock
        {
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        var error = new TextBlock { Foreground = (Brush)Application.Current.Resources["DangerBrush"], TextWrapping = TextWrapping.Wrap };
        var busy = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        newBox.PasswordChanged += (_, _) =>
        {
            var r = PasswordStrength.Evaluate(newBox.Password);
            strength.Text = r.Level == StrengthLevel.None ? "" : $"Stärke: {r.Label} (≈ {r.EntropyBits:0} Bit)";
        };

        var panel = new StackPanel { Spacing = 12, Width = 380 };
        foreach (var child in new UIElement[] { currentBox, newBox, strength, confirmBox, error, busy })
            panel.Children.Add(child);

        var dialog = new ContentDialog
        {
            Title = "Master-Passwort ändern",
            Content = panel,
            PrimaryButtonText = "Ändern",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (d, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                error.Text = "";
                if (newBox.Password.Length < 8) { error.Text = "Mindestens 8 Zeichen erforderlich."; args.Cancel = true; return; }
                if (newBox.Password != confirmBox.Password) { error.Text = "Die neuen Passwörter stimmen nicht überein."; args.Cancel = true; return; }

                busy.Visibility = Visibility.Visible;
                d.IsPrimaryButtonEnabled = false;
                var current = currentBox.Password;
                var next = newBox.Password;

                if (!await Task.Run(() => App.Store.VerifyPassword(current)))
                {
                    error.Text = "Das aktuelle Master-Passwort ist falsch.";
                    Motion.Shake(currentBox);
                    args.Cancel = true;
                    return;
                }

                await Task.Run(() => App.Store.ChangeMasterPassword(next));

                if (App.Settings.Current.WindowsHelloEnabled && WindowsHelloService.HasStoredPassword())
                    WindowsHelloService.StorePassword(next);
            }
            catch (Exception ex)
            {
                error.Text = "Fehler: " + ex.Message;
                args.Cancel = true;
            }
            finally
            {
                busy.Visibility = Visibility.Collapsed;
                d.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };

        if (await App.MainWindowInstance.ShowDialogAsync(dialog) == ContentDialogResult.Primary)
            App.MainWindowInstance.ShowToast("Master-Passwort wurde geändert", "");
    }

    private async void WindowsHello_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        if (!WindowsHelloToggle.IsOn)
        {
            WindowsHelloService.RemoveStoredPassword();
            App.Settings.Current.WindowsHelloEnabled = false;
            App.Settings.Save();
            App.MainWindowInstance.ShowToast("Windows Hello deaktiviert", "");
            return;
        }

        var pwBox = new PasswordBox { PlaceholderText = "Aktuelles Master-Passwort", Width = 340 };
        var dialog = new ContentDialog
        {
            Title = "Windows Hello aktivieren",
            Content = pwBox,
            PrimaryButtonText = "Aktivieren",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        };

        var ok = await App.MainWindowInstance.ShowDialogAsync(dialog) == ContentDialogResult.Primary
                 && !string.IsNullOrEmpty(pwBox.Password);
        var password = pwBox.Password;

        if (ok && !await Task.Run(() => App.Store.VerifyPassword(password)))
        {
            ok = false;
            await App.MainWindowInstance.ShowMessageAsync("Falsches Passwort", "Das Master-Passwort ist nicht korrekt.");
        }
        if (ok) ok = await WindowsHelloService.AuthenticateAsync();

        if (!ok)
        {
            _loading = true;
            WindowsHelloToggle.IsOn = false;
            _loading = false;
            return;
        }

        WindowsHelloService.StorePassword(password);
        App.Settings.Current.WindowsHelloEnabled = true;
        App.Settings.Save();
        App.MainWindowInstance.ShowToast("Windows Hello aktiviert", "");
    }

    // ================= Browser-Erweiterung =================

    private void Bridge_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Current.BrowserBridgeEnabled = BridgeToggle.IsOn;
        App.Settings.Save();
        if (BridgeToggle.IsOn) App.Bridge.Start();
        else App.Bridge.Stop();
        UpdateBridgeStatus();
    }

    private void UpdateBridgeStatus()
    {
        var bridge = App.Bridge;
        string text;
        string color;
        if (bridge.IsRunning)
        {
            text = $"Aktiv auf localhost:{BrowserBridgeService.Port}";
            if (bridge.LastContact is DateTime seen)
                text += $" · zuletzt verbunden {seen:HH:mm} Uhr";
            color = "SuccessBrush";
        }
        else if (!App.Settings.Current.BrowserBridgeEnabled)
        {
            text = "Ausgeschaltet";
            color = "TextFillColorTertiaryBrush";
        }
        else
        {
            text = bridge.LastError ?? "Nicht gestartet";
            color = "DangerBrush";
        }
        BridgeStatusText.Text = text;
        BridgeStatusDot.Fill = (Brush)Application.Current.Resources[color];

        bool paired = bridge.IsPaired;
        PairingText.Text = paired ? "Mindestens ein Browser ist gekoppelt" : "Noch kein Browser gekoppelt";
        UnpairButton.Visibility = paired ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Unpair_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = await App.MainWindowInstance.ConfirmAsync(
            "Kopplung aufheben?",
            "Alle gekoppelten Browser verlieren sofort den Zugriff und müssen neu gekoppelt werden.",
            "Aufheben", danger: true);
        if (!confirmed) return;
        App.Bridge.RevokePairing();
        UpdateBridgeStatus();
        App.MainWindowInstance.ShowToast("Kopplung aufgehoben", "");
    }

    private async void InstallExtension_Click(object sender, RoutedEventArgs e)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "BrowserExtension");
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PasswordVault Browser-Erweiterung");

        try
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException("Die Erweiterungsdateien fehlen im App-Paket.");
            CopyDirectory(source, target);
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Einrichtung fehlgeschlagen", ex.Message);
            return;
        }

        await Windows.System.Launcher.LaunchFolderPathAsync(target);

        var steps = new StackPanel { Spacing = 10, Width = 420 };
        string[] lines =
        {
            "1.  Öffne im Browser die Erweiterungsseite:\n     chrome://extensions  ·  edge://extensions  ·  opera://extensions",
            "2.  Aktiviere den „Entwicklermodus“.",
            "3.  Klicke „Entpackte Erweiterung laden“ und wähle den geöffneten Ordner:",
        };
        foreach (var line in lines)
            steps.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        steps.Children.Add(new TextBox { Text = target, IsReadOnly = true });
        steps.Children.Add(new TextBlock
        {
            Text = "4.  Klicke auf das Password-Vault-Symbol in der Symbolleiste und dann auf „Mit App koppeln“. Bestätige den Code hier in der App.",
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = "Browser-Erweiterung einrichten",
            Content = steps,
            PrimaryButtonText = "Pfad kopieren",
            CloseButtonText = "Fertig",
            DefaultButton = ContentDialogButton.Close
        };
        if (await App.MainWindowInstance.ShowDialogAsync(dialog) == ContentDialogResult.Primary)
            ClipboardHelper.CopyPlain(target, "Pfad");
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    // ================= Generator =================

    private void Generator_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || LengthValue == null) return;
        var s = App.Settings.Current;
        s.DefaultPasswordLength = (int)LengthSlider.Value;
        s.UseUppercase = UpperToggle.IsOn;
        s.UseLowercase = LowerToggle.IsOn;
        s.UseDigits = DigitToggle.IsOn;
        s.UseSymbols = SymbolToggle.IsOn;
        s.ExcludeAmbiguous = AmbiguousToggle.IsOn;

        // Mindestens eine Zeichengruppe muss aktiv bleiben
        if (!s.UseUppercase && !s.UseLowercase && !s.UseDigits && !s.UseSymbols)
        {
            _loading = true;
            LowerToggle.IsOn = s.UseLowercase = true;
            _loading = false;
        }

        LengthValue.Text = s.DefaultPasswordLength.ToString();
        App.Settings.Save();
        UpdateGeneratorSummary();
    }

    private void UpdateGeneratorSummary()
    {
        var s = App.Settings.Current;
        var parts = new List<string>();
        if (s.UseUppercase) parts.Add("A–Z");
        if (s.UseLowercase) parts.Add("a–z");
        if (s.UseDigits) parts.Add("0–9");
        if (s.UseSymbols) parts.Add("!@#");
        GeneratorSummary.Text = $"{s.DefaultPasswordLength} Zeichen · {string.Join(" ", parts)}";
    }

    // ================= Darstellung =================

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem item) return;
        App.Settings.Current.Theme = (string)item.Tag;
        App.Settings.Save();
        App.MainWindowInstance.ApplyTheme(App.Settings.Current.Theme);
    }

    private void Animations_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Current.AnimationsEnabled = AnimationsToggle.IsOn;
        App.Settings.Save();
    }

    // ================= Daten =================

    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Add(".csv");
        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var csv = await File.ReadAllTextAsync(file.Path);
            var imported = new CsvService().Import(csv);
            if (imported.Count == 0)
            {
                await App.MainWindowInstance.ShowMessageAsync("Keine Einträge gefunden",
                    "Die Datei enthält keine erkennbaren Zugangsdaten. Unterstützt werden u.a. Exporte aus Chrome, Edge, Firefox, Bitwarden und KeePass.");
                return;
            }

            // Duplikate (gleicher Titel, Benutzer und Passwort) überspringen
            var existing = App.Store.GetEntries()
                .Select(x => (x.Title, x.Username, x.Password))
                .ToHashSet();
            var fresh = imported.Where(x => !existing.Contains((x.Title, x.Username, x.Password))).ToList();
            var duplicates = imported.Count - fresh.Count;

            if (fresh.Count == 0)
            {
                await App.MainWindowInstance.ShowMessageAsync("Bereits vorhanden",
                    $"Alle {imported.Count} Einträge sind schon im Tresor.");
                return;
            }

            var message = $"{fresh.Count} neue Einträge gefunden.";
            if (duplicates > 0) message += $" {duplicates} Duplikate werden übersprungen.";
            message += "\n\nTipp: Lösche die CSV-Datei nach dem Import – sie ist unverschlüsselt.";

            if (!await App.MainWindowInstance.ConfirmAsync("Import bestätigen", message, "Importieren"))
                return;

            App.Store.AddRange(fresh);
            App.MainWindowInstance.ShowToast($"{fresh.Count} Einträge importiert", "");
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Import fehlgeschlagen", ex.Message);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = await App.MainWindowInstance.ConfirmAsync(
            "Unverschlüsselt exportieren?",
            "Die CSV-Datei enthält alle Passwörter im Klartext. Jeder mit Zugriff auf die Datei kann sie lesen. Für Sicherungen ist das verschlüsselte Backup die bessere Wahl.",
            "Trotzdem exportieren", danger: true);
        if (!confirmed) return;

        var picker = new Windows.Storage.Pickers.FileSavePicker();
        InitializePicker(picker);
        picker.SuggestedFileName = $"passwoerter_{DateTime.Now:yyyy-MM-dd}";
        picker.FileTypeChoices.Add("CSV-Datei", new List<string> { ".csv" });
        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var entries = App.Store.GetEntries();
            await File.WriteAllTextAsync(file.Path, new CsvService().Export(entries), new System.Text.UTF8Encoding(true));
            App.MainWindowInstance.ShowToast($"{entries.Count} Einträge exportiert", "");
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Export fehlgeschlagen", ex.Message);
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        InitializePicker(picker);
        picker.SuggestedFileName = $"vault_backup_{DateTime.Now:yyyy-MM-dd}";
        picker.FileTypeChoices.Add("Password-Vault-Backup", new List<string> { ".dat" });
        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            App.Store.Backup(file.Path);
            App.MainWindowInstance.ShowToast("Verschlüsseltes Backup gespeichert", "");
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Backup fehlgeschlagen", ex.Message);
        }
    }

    private void CopyVaultPath_Click(object sender, RoutedEventArgs e)
        => ClipboardHelper.CopyPlain(App.VaultPath, "Pfad");

    private async void OpenVaultFolder_Click(object sender, RoutedEventArgs e)
        => await Windows.System.Launcher.LaunchFolderPathAsync(App.DataDirectory);

    // ================= Hilfen =================

    private static void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private static void SelectByTag(ComboBox combo, string tag)
    {
        var match = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == tag);
        combo.SelectedItem = match ?? combo.Items.FirstOrDefault();
    }
}

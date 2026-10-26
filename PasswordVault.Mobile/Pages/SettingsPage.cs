namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Sicherheit (Face ID, Sperren, Zwischenablage, Master-Passwort), Abgleich mit dem PC, CSV, Darstellung.
/// </summary>
public class SettingsPage : ContentPage
{
    private readonly Switch _biometric = new();
    private readonly Entry _biometricPassword = new() { IsPassword = true, Placeholder = "Master-Passwort", ReturnType = ReturnType.Done };
    private readonly VerticalStackLayout _biometricForm = new() { Spacing = 10, IsVisible = false };
    private bool _loading;

    public SettingsPage()
    {
        Title = "Einstellungen";

        // ===== Sicherheit =====
        var securityRows = new VerticalStackLayout { Spacing = 4 };
        if (Biometrics.IsAvailable)
        {
            _biometric.IsToggled = Biometrics.IsEnabled;
            _biometric.Toggled += async (_, e) => await BiometricToggledAsync(e.Value);
            securityRows.Add(Ui.SettingRow(Biometrics.Name, "Tresor ohne Master-Passwort entsperren", _biometric));

            var activate = Ui.PrimaryButton($"{Biometrics.Name} aktivieren");
            activate.Clicked += async (_, _) => await EnableBiometricAsync();
            _biometricPassword.Completed += async (_, _) => await EnableBiometricAsync();
            _biometricForm.Add(Ui.Field("Zur Bestätigung", _biometricPassword));
            _biometricForm.Add(activate);
        }
        securityRows.Add(Ui.SettingRow("Automatisch sperren", "wenn die App im Hintergrund ist",
            ChoicePicker(MobileSettings.LockChoices, MobileSettings.LockAfterSeconds, v => MobileSettings.LockAfterSeconds = v)));
        securityRows.Add(Ui.SettingRow("Zwischenablage leeren", "nach dem Kopieren eines Passworts",
            ChoicePicker(MobileSettings.ClipboardChoices, MobileSettings.ClipboardSeconds, v => MobileSettings.ClipboardSeconds = v)));

        var current = new Entry { IsPassword = true, Placeholder = "Aktuelles Passwort" };
        var next = new Entry { IsPassword = true, Placeholder = "Neues Passwort (mind. 8 Zeichen)" };
        var repeat = new Entry { IsPassword = true, Placeholder = "Neues Passwort wiederholen" };
        var (meter, updateMeter) = Ui.StrengthMeter();
        next.TextChanged += (_, e) => updateMeter(e.NewTextValue ?? "");
        var confirm = Ui.PrimaryButton("Passwort ändern");
        var form = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children = { Ui.Field("Aktuell", current), Ui.Field("Neu", next), meter, Ui.Field("Wiederholen", repeat), confirm }
        };
        confirm.Clicked += async (_, _) => await ChangePasswordAsync(current, next, repeat, form, confirm);

        var changePassword = Ui.SecondaryButton("Master-Passwort ändern …");
        changePassword.Clicked += (_, _) =>
        {
            form.IsVisible = !form.IsVisible;
            if (form.IsVisible) _ = Ui.PopIn(form);
        };

        // ===== Abgleich =====
        var export = Ui.SecondaryButton("Tresor sichern / an PC senden …");
        export.Clicked += async (_, _) => await VaultTransfer.ExportAsync();

        var import = Ui.SecondaryButton("Tresor vom PC übernehmen …");
        import.Clicked += async (_, _) =>
        {
            if (await VaultTransfer.ImportAsync(this))
                App.ShowUnlock();
        };

        // ===== Daten =====
        var importCsv = Ui.SecondaryButton("CSV importieren …");
        importCsv.Clicked += async (_, _) =>
        {
            try
            {
                var count = await VaultTransfer.ImportCsvAsync(this);
                if (count > 0) Ui.Toast(this, $"{count} Einträge importiert");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Import fehlgeschlagen", ex.Message, "OK");
            }
        };
        var exportCsv = Ui.SecondaryButton("Als CSV exportieren …", Ui.Danger);
        exportCsv.Clicked += async (_, _) =>
        {
            try
            {
                await VaultTransfer.ExportCsvAsync(this);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Export fehlgeschlagen", ex.Message, "OK");
            }
        };

        // ===== Darstellung: eigene Einstellung zusätzlich zu iOS "Bewegung reduzieren" =====
        var motion = new Switch { IsToggled = Ui.ReduceMotionSetting };
        motion.Toggled += (_, e) => Ui.ReduceMotion = e.Value;

        var lockNow = Ui.PrimaryButton("Jetzt sperren");
        lockNow.Clicked += (_, _) => App.ShowUnlock();

        var views = new View[]
        {
            Ui.Section("Sicherheit"),
            Ui.Card(securityRows),
            _biometricForm,
            changePassword,
            form,
            Ui.Section("Abgleich mit dem PC"),
            Ui.Card(Ui.Hint(
                "Die Tresor-Datei ist verschlüsselt und kann über iCloud Drive, OneDrive oder AirDrop "
                + "zwischen PC und iPhone kopiert werden.\n\n"
                + "• Vom PC: Am PC unter Einstellungen beim Speicherort auf „Ordner öffnen“ klicken, vault.dat in die Cloud legen, "
                + "dann hier „Tresor vom PC übernehmen“.\n"
                + "• Zum PC: „Tresor sichern“ → „In Dateien sichern“. Am PC die App sperren, vault.dat im Datenordner ersetzen, entsperren.\n\n"
                + "Wichtig: Immer nur auf einem Gerät ändern und danach abgleichen – beim Ersetzen gehen Änderungen auf dem anderen Gerät verloren.")),
            export,
            import,
            Ui.Section("Daten"),
            Ui.Card(Ui.Hint("Import aus Chrome, Edge, Firefox, Bitwarden, 1Password und KeePass. Der CSV-Export ist unverschlüsselt.")),
            importCsv,
            exportCsv,
            Ui.Section("Darstellung"),
            Ui.Card(Ui.SettingRow("Animationen reduzieren", "Schaltet Bewegungen fast komplett ab.", motion)),
            lockNow,
            new Label
            {
                Text = $"Password Vault für iPhone {AppInfo.Current.VersionString}",
                TextColor = Ui.Muted,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10)
            }
        };

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 8, 16, 40) };
        foreach (var v in views) stack.Add(v);
        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(views);
    }

    private static Picker ChoicePicker((string Label, int Seconds)[] choices, int current, Action<int> save)
    {
        var picker = new Picker
        {
            ItemsSource = choices.Select(c => c.Label).ToList(),
            TextColor = Ui.Text,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.End,
            MinimumWidthRequest = 150
        };
        var index = Array.FindIndex(choices, c => c.Seconds == current);
        picker.SelectedIndex = index >= 0 ? index : 0;
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex >= 0) save(choices[picker.SelectedIndex].Seconds);
        };
        return picker;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Nicht bestätigtes Einschalten zurücknehmen
        _loading = true;
        _biometric.IsToggled = Biometrics.IsEnabled;
        _loading = false;
        _biometricForm.IsVisible = false;
    }

    private async Task BiometricToggledAsync(bool on)
    {
        if (_loading) return;

        if (on)
        {
            // Erst nach Bestätigung mit dem Master-Passwort wirklich aktiv
            _biometricPassword.Text = "";
            _biometricForm.IsVisible = true;
            await Ui.PopIn(_biometricForm);
            _biometricPassword.Focus();
            return;
        }

        _biometricForm.IsVisible = false;
        if (!MobileSettings.BiometricEnabled) return;
        Biometrics.Remove();
        MobileSettings.BiometricEnabled = false;
        Ui.Toast(this, $"{Biometrics.Name} deaktiviert");
    }

    private async Task EnableBiometricAsync()
    {
        var password = _biometricPassword.Text ?? "";
        if (!await Task.Run(() => App.Store.VerifyPassword(password)))
        {
            Ui.Toast(this, "Master-Passwort ist falsch", error: true);
            await Ui.Shake(_biometricForm);
            return;
        }
        if (!Biometrics.Store(password))
        {
            Ui.Toast(this, $"{Biometrics.Name} konnte nicht eingerichtet werden", error: true);
            return;
        }

        MobileSettings.BiometricEnabled = true;
        _biometricPassword.Text = "";
        _biometricForm.IsVisible = false;
        _biometricPassword.Unfocus();
        Ui.Toast(this, $"{Biometrics.Name} aktiviert");
    }

    private async Task ChangePasswordAsync(Entry current, Entry next, Entry repeat, View form, Button confirm)
    {
        if ((next.Text ?? "").Length < 8)
        {
            Ui.Toast(this, "Mindestens 8 Zeichen", error: true);
            return;
        }
        if (next.Text != repeat.Text)
        {
            Ui.Toast(this, "Passwörter stimmen nicht überein", error: true);
            return;
        }

        confirm.IsEnabled = false;
        confirm.Text = "Einen Moment …";
        try
        {
            if (!await Task.Run(() => App.Store.VerifyPassword(current.Text ?? "")))
            {
                Ui.Toast(this, "Aktuelles Passwort ist falsch", error: true);
                await Ui.Shake(form);
                return;
            }

            var pw = next.Text!;
            await Task.Run(() => App.Store.ChangeMasterPassword(pw));
            if (Biometrics.IsEnabled) Biometrics.Store(pw);
        }
        finally
        {
            confirm.IsEnabled = true;
            confirm.Text = "Passwort ändern";
        }

        current.Text = next.Text = repeat.Text = "";
        form.IsVisible = false;
        Ui.Toast(this, "Master-Passwort geändert");
        await DisplayAlertAsync("Hinweis",
            "Der Tresor auf deinem PC hat noch das alte Passwort, bis du ihn mit dieser Datei ersetzt.", "OK");
    }
}

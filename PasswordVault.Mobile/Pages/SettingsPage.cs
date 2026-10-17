namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Tresor-Datei mit dem PC austauschen, Master-Passwort ändern.
/// </summary>
public class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Einstellungen";

        var export = Ui.SecondaryButton("Tresor sichern / an PC senden …");
        export.Clicked += async (_, _) => await VaultTransfer.ExportAsync();

        var import = Ui.SecondaryButton("Tresor vom PC übernehmen …");
        import.Clicked += async (_, _) =>
        {
            if (await VaultTransfer.ImportAsync(this))
                App.ShowUnlock();
        };

        var current = new Entry { IsPassword = true, Placeholder = "Aktuelles Passwort" };
        var next = new Entry { IsPassword = true, Placeholder = "Neues Passwort (mind. 8 Zeichen)" };
        var repeat = new Entry { IsPassword = true, Placeholder = "Neues Passwort wiederholen" };
        var confirm = Ui.PrimaryButton("Passwort ändern");
        var form = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children = { Ui.Field("Aktuell", current), Ui.Field("Neu", next), Ui.Field("Wiederholen", repeat), confirm }
        };
        confirm.Clicked += async (_, _) => await ChangePasswordAsync(current, next, repeat, form);

        var changePassword = Ui.SecondaryButton("Master-Passwort ändern …");
        changePassword.Clicked += (_, _) =>
        {
            form.IsVisible = !form.IsVisible;
            if (form.IsVisible) Ui.AnimateIn(form);
        };

        // Eigene Einstellung zusätzlich zu iOS "Bewegung reduzieren"
        var motion = new Switch { IsToggled = Ui.ReduceMotionSetting, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        motion.Toggled += (_, e) => Ui.ReduceMotion = e.Value;
        var motionRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        motionRow.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "Animationen reduzieren", FontSize = 16 },
                new Label { Text = "Schaltet Bewegungen fast komplett ab.", FontSize = 13, TextColor = Ui.Muted }
            }
        }, 0, 0);
        motionRow.Add(motion, 1, 0);

        var lockNow = Ui.PrimaryButton("Jetzt sperren");
        lockNow.Clicked += (_, _) => App.ShowUnlock();

        var views = new View[]
        {
            Section("Abgleich mit dem PC"),
            Ui.Card(new Label
            {
                Text = "Die Tresor-Datei ist verschlüsselt und kann über iCloud Drive, OneDrive oder AirDrop "
                     + "zwischen PC und iPhone kopiert werden.\n\n"
                     + "• Vom PC: Am PC unter Einstellungen beim Speicherort auf „Ordner öffnen“ klicken, vault.dat in die Cloud legen, "
                     + "dann hier „Tresor vom PC übernehmen“.\n"
                     + "• Zum PC: „Tresor sichern“ → „In Dateien sichern“. Am PC die App sperren, vault.dat im Datenordner ersetzen, entsperren.\n\n"
                     + "Wichtig: Immer nur auf einem Gerät ändern und danach abgleichen – beim Ersetzen gehen Änderungen auf dem anderen Gerät verloren.",
                TextColor = Ui.Muted,
                FontSize = 14
            }),
            export,
            import,
            Section("Sicherheit"),
            changePassword,
            form,
            Ui.Card(new Label
            {
                Text = "Die App sperrt sich, wenn sie länger als 60 Sekunden im Hintergrund war. "
                     + "Kopierte Passwörter löscht iOS nach 30 Sekunden aus der Zwischenablage und sie werden nicht an andere Apple-Geräte übertragen.",
                TextColor = Ui.Muted,
                FontSize = 14
            }),
            Section("Darstellung"),
            Ui.Card(motionRow),
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

    private static Label Section(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontFamily = Ui.FontBold,
        Margin = new Thickness(4, 12, 0, 0)
    };

    private async Task ChangePasswordAsync(Entry current, Entry next, Entry repeat, View form)
    {
        if (!await Task.Run(() => App.Store.VerifyPassword(current.Text ?? "")))
        {
            Ui.Toast(this, "Aktuelles Passwort ist falsch", error: true);
            await Ui.Shake(form);
            return;
        }
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

        var pw = next.Text!;
        await Task.Run(() => App.Store.ChangeMasterPassword(pw));
        current.Text = next.Text = repeat.Text = "";
        form.IsVisible = false;
        Ui.Toast(this, "Master-Passwort geändert");
        await DisplayAlertAsync("Hinweis",
            "Der Tresor auf deinem PC hat noch das alte Passwort, bis du ihn mit dieser Datei ersetzt.", "OK");
    }
}

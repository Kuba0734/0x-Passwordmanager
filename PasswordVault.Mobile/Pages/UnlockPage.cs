using System.Security.Cryptography;
using Microsoft.Maui.Controls.Shapes;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Entsperren – oder beim ersten Start: neuen Tresor anlegen bzw. den Tresor vom PC übernehmen.
/// </summary>
public class UnlockPage : ContentPage
{
    private readonly bool _create = !App.Store.Exists;
    private readonly Entry _password = new() { IsPassword = true, Placeholder = "Master-Passwort", ReturnType = ReturnType.Go };
    private readonly Entry _repeat = new() { IsPassword = true, Placeholder = "Wiederholen", ReturnType = ReturnType.Go };
    private readonly Label _error = new() { TextColor = Ui.Danger, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center, IsVisible = false };
    private readonly Button _go;
    private readonly ActivityIndicator _busy = new() { Color = Ui.Violet, IsRunning = false, HeightRequest = 24 };
    private readonly Border _logo;
    private readonly View _form;

    public UnlockPage()
    {
        _logo = new Border
        {
            WidthRequest = 96,
            HeightRequest = 96,
            HorizontalOptions = LayoutOptions.Center,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 30 },
            Background = Ui.AccentGradient,
            Shadow = new Shadow { Brush = new SolidColorBrush(Ui.Violet), Radius = 40, Opacity = 0.55f, Offset = new Point(0, 10) },
            Content = new Label { Text = "🔒", FontSize = 44, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };

        var title = new Label
        {
            Text = _create ? "Willkommen" : "Password Vault",
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var subtitle = new Label
        {
            Text = _create
                ? "Lege einen neuen Tresor an oder übernimm den Tresor von deinem PC."
                : "Gib dein Master-Passwort ein.",
            TextColor = Ui.Muted,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _go = Ui.PrimaryButton(_create ? "Tresor anlegen" : "Entsperren");
        _go.Clicked += async (_, _) => await SubmitAsync();
        _password.Completed += async (_, _) => { if (_create) _repeat.Focus(); else await SubmitAsync(); };
        _repeat.Completed += async (_, _) => await SubmitAsync();

        var fields = new VerticalStackLayout { Spacing = 10, Children = { Ui.Field("Master-Passwort", _password) } };
        if (_create)
        {
            fields.Add(Ui.Field("Passwort wiederholen", _repeat));
            fields.Add(new Label
            {
                Text = "Das Master-Passwort kann nicht wiederhergestellt werden. Wenn du es vergisst, sind alle Daten verloren.",
                TextColor = Ui.Muted,
                FontSize = 13
            });
        }

        var form = new VerticalStackLayout { Spacing = 14, Children = { fields, _error, _go, _busy } };
        if (_create)
        {
            var import = Ui.SecondaryButton("Tresor vom PC übernehmen …");
            import.Clicked += async (_, _) =>
            {
                if (await VaultTransfer.ImportAsync(this))
                    App.ShowUnlock(); // Seite neu aufbauen, jetzt im Entsperren-Modus
            };
            form.Add(import);
        }
        _form = form;

        var stack = new VerticalStackLayout
        {
            Spacing = 18,
            Padding = new Thickness(28, 0),
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 480,
            Children = { _logo, title, subtitle, _form }
        };

        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(_logo, title, subtitle, _form);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _password.Focus();
        _ = Breathe();
    }

    // Logo "atmet" leicht, solange die Seite offen ist
    private async Task Breathe()
    {
        await Task.Delay(600);
        while (Handler != null && !_busy.IsRunning)
        {
            await _logo.ScaleToAsync(1.05, 1400, Easing.SinInOut);
            await _logo.ScaleToAsync(1.0, 1400, Easing.SinInOut);
        }
    }

    private async Task SubmitAsync()
    {
        if (_busy.IsRunning) return;
        var pw = _password.Text ?? "";

        if (_create)
        {
            if (pw.Length < 8) { await ShowError("Mindestens 8 Zeichen."); return; }
            if (pw != _repeat.Text) { await ShowError("Die Passwörter stimmen nicht überein."); return; }
        }
        else if (pw.Length == 0) { await ShowError("Bitte Passwort eingeben."); return; }

        SetBusy(true);
        try
        {
            // Argon2 braucht ein paar Sekunden – nicht im UI-Thread
            await Task.Run(() =>
            {
                if (_create) App.Store.Create(pw);
                else App.Store.Unlock(pw);
            });
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            await Task.WhenAll(_logo.ScaleToAsync(1.25, 180), _form.FadeToAsync(0, 180));
            App.ShowVault();
        }
        catch (CryptographicException)
        {
            SetBusy(false);
            _password.Text = "";
            await ShowError("Falsches Master-Passwort.");
            _password.Focus();
        }
        catch (Exception ex)
        {
            SetBusy(false);
            await ShowError("Fehler: " + ex.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy.IsRunning = busy;
        _go.IsEnabled = !busy;
        _password.IsEnabled = _repeat.IsEnabled = !busy;
        _go.Text = busy ? "Einen Moment …" : _create ? "Tresor anlegen" : "Entsperren";
        if (busy) _error.IsVisible = false;
    }

    private async Task ShowError(string text)
    {
        _error.Text = text;
        _error.IsVisible = true;
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        await Ui.Shake(_form);
    }
}

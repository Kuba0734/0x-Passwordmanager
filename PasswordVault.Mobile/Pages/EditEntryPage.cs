using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Neuer Eintrag (<c>existing == null</c>) oder Bearbeiten, inklusive Passwort-Generator.
/// </summary>
public class EditEntryPage : ContentPage
{
    private readonly PasswordEntry _entry;
    private readonly bool _isNew;

    private readonly Entry _title = new() { Placeholder = "z. B. Amazon" };
    private readonly Entry _username = new() { Placeholder = "E-Mail oder Name", Keyboard = Keyboard.Email };
    private readonly Entry _password = new() { Placeholder = "Passwort", IsPassword = true, FontFamily = "Menlo" };
    private readonly Entry _url = new() { Placeholder = "https://…", Keyboard = Keyboard.Url };
    private readonly Editor _notes = new() { Placeholder = "Notizen", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60 };
    private readonly Picker _category = new() { Title = "Kategorie", TextColor = Ui.Text, TitleColor = Ui.Muted, ItemsSource = Categories.All };
    private readonly Switch _favorite = new();
    private readonly Slider _length = new(8, 64, 20);
    private readonly Switch _symbols = new() { IsToggled = true };

    public EditEntryPage(PasswordEntry? existing)
    {
        _isNew = existing == null;
        _entry = existing?.Clone() ?? new PasswordEntry();
        Title = _isNew ? "Neuer Eintrag" : "Bearbeiten";
        ToolbarItems.Add(new ToolbarItem("Sichern", null, Save));

        _title.Text = _entry.Title;
        _username.Text = _entry.Username;
        _password.Text = _entry.Password;
        _url.Text = _entry.Url;
        _notes.Text = _entry.Notes;
        _category.SelectedItem = Categories.All.Contains(_entry.Category) ? _entry.Category : Categories.General;
        _favorite.IsToggled = _entry.IsFavorite;
        foreach (var e in new[] { _username, _url }) e.IsSpellCheckEnabled = e.IsTextPredictionEnabled = false;
        _password.IsSpellCheckEnabled = _password.IsTextPredictionEnabled = false;

        var (meter, updateMeter) = Ui.StrengthMeter();
        updateMeter(_entry.Password);
        _password.TextChanged += (_, e) => updateMeter(e.NewTextValue ?? "");

        var show = Ui.IconButton("👁", "Passwort anzeigen", (_, _) => { });
        show.Clicked += (_, _) =>
        {
            _password.IsPassword = !_password.IsPassword;
            show.Text = _password.IsPassword ? "👁" : "🙈";
        };
        var generate = Ui.IconButton("🎲", "Passwort erzeugen", async (_, _) => await GenerateAsync());

        var lengthLabel = new Label { Text = "Länge 20", FontSize = 14, TextColor = Ui.Muted, VerticalOptions = LayoutOptions.Center };
        _length.ValueChanged += (_, e) => lengthLabel.Text = $"Länge {(int)e.NewValue}";
        var generator = Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "GENERATOR", FontSize = 11, TextColor = Ui.Muted, CharacterSpacing = 1 },
                new Grid
                {
                    ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
                    ColumnSpacing = 10,
                    Children = { lengthLabel, WithColumn(_length, 1) }
                },
                Row("Sonderzeichen", _symbols)
            }
        });

        var views = new List<View>
        {
            Ui.Field("Titel", _title),
            Ui.Field("Benutzername", _username),
            Ui.Field("Passwort", _password, show, generate),
            meter,
            generator,
            Ui.Field("Website", _url),
            Ui.Card(new VerticalStackLayout { Spacing = 4, Children = { Row("Kategorie", _category), Row("Favorit", _favorite) } }),
            Ui.Field("Notizen", _notes)
        };

        var save = Ui.PrimaryButton(_isNew ? "Eintrag anlegen" : "Änderungen sichern");
        save.Clicked += (_, _) => Save();
        views.Add(save);

        if (!_isNew)
        {
            var delete = Ui.SecondaryButton("Eintrag löschen", Ui.Danger);
            delete.Clicked += async (_, _) => await DeleteAsync();
            views.Add(delete);
        }

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 12, 16, 40) };
        foreach (var v in views) stack.Add(v);
        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(views.ToArray());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_isNew) _title.Focus();
    }

    private static View WithColumn(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    private static Grid Row(string label, View control)
    {
        control.HorizontalOptions = LayoutOptions.End;
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, FontSize = 16, VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(control, 1, 0);
        return grid;
    }

    private async Task GenerateAsync()
    {
        if (!string.IsNullOrEmpty(_password.Text) && !_isNew
            && !await DisplayAlertAsync("Passwort ersetzen?", "Das aktuelle Passwort wird überschrieben.", "Ersetzen", "Abbrechen"))
            return;

        _password.Text = new PasswordGenerator().Generate((int)_length.Value, useSymbols: _symbols.IsToggled);
        _password.IsPassword = false;
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        await _password.ScaleToAsync(1.04, 90);
        await _password.ScaleToAsync(1, 140, Easing.SpringOut);
    }

    private async void Save()
    {
        var title = _title.Text?.Trim() ?? "";
        if (title.Length == 0)
        {
            Ui.Toast(this, "Bitte einen Titel eingeben", error: true);
            await Ui.Shake(_title);
            return;
        }

        _entry.Title = title;
        _entry.Username = _username.Text?.Trim() ?? "";
        _entry.Password = _password.Text ?? "";
        _entry.Url = _url.Text?.Trim() ?? "";
        _entry.Notes = _notes.Text ?? "";
        _entry.Category = _category.SelectedItem as string ?? Categories.General;
        _entry.IsFavorite = _favorite.IsToggled;

        try
        {
            if (_isNew) App.Store.Add(_entry);
            else App.Store.Update(_entry);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Speichern fehlgeschlagen", ex.Message, "OK");
            return;
        }

        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        await Navigation.PopAsync();
    }

    private async Task DeleteAsync()
    {
        if (!await DisplayAlertAsync("Eintrag löschen?", $"„{_entry.Title}“ wird endgültig gelöscht.", "Löschen", "Abbrechen"))
            return;
        App.Store.Delete(_entry.Id);
        await Navigation.PopToRootAsync();
    }
}

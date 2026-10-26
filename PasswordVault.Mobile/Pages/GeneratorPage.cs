using System.Security.Cryptography;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Passwort-Generator wie am PC: Optionen, Stärke mit Knackzeit, Verlauf dieser Sitzung.
/// </summary>
public class GeneratorPage : ContentPage
{
    // Sitzungsverlauf – lebt nur bis zum Sperren
    private static readonly List<string> History = new();

    private readonly PasswordGenerator _generator = new();
    private readonly Label _password = new() { FontFamily = Ui.FontMono, FontSize = 22, LineBreakMode = LineBreakMode.CharacterWrap, HorizontalTextAlignment = TextAlignment.Center, MinimumHeightRequest = 60 };
    private readonly Label _crackTime = new() { FontSize = 12, TextColor = Ui.Muted, Padding = new Thickness(4, 0) };
    private readonly Action<string> _updateMeter;
    private readonly Slider _length = new(8, 64, MobileSettings.Length);
    private readonly Switch _upper = new() { IsToggled = MobileSettings.Upper };
    private readonly Switch _lower = new() { IsToggled = MobileSettings.Lower };
    private readonly Switch _digits = new() { IsToggled = MobileSettings.Digits };
    private readonly Switch _symbols = new() { IsToggled = MobileSettings.Symbols };
    private readonly Switch _ambiguous = new() { IsToggled = MobileSettings.ExcludeAmbiguous };
    private readonly VerticalStackLayout _history = new() { Spacing = 4 };
    private readonly Label _historyEmpty = Ui.Hint("Kopierte Passwörter erscheinen hier. Wird beim Sperren gelöscht.");
    private string _current = "";
    private bool _initializing = true;

    public static void ClearHistory() => History.Clear();

    public GeneratorPage()
    {
        Title = "Generator";

        var (meter, update) = Ui.StrengthMeter();
        _updateMeter = update;

        var lengthLabel = new Label { Text = $"Länge {(int)_length.Value}", FontSize = 16, VerticalOptions = LayoutOptions.Center, WidthRequest = 90 };
        _length.ValueChanged += (_, e) =>
        {
            var value = (int)Math.Round(e.NewValue);
            lengthLabel.Text = $"Länge {value}";
            // Nur bei ganzen Schritten neu erzeugen, sonst flackert es beim Ziehen
            if (value != _current.Length) Generate();
        };
        foreach (var s in new[] { _upper, _lower, _digits, _symbols, _ambiguous })
            s.Toggled += (_, _) => Generate();

        var regenerate = Ui.PrimaryButton("Neu erzeugen");
        regenerate.Clicked += (_, _) => Generate();
        var copy = Ui.SecondaryButton("Kopieren");
        copy.Clicked += (_, _) => Copy();
        var save = Ui.SecondaryButton("Als Eintrag speichern …");
        save.Clicked += async (_, _) =>
        {
            AddToHistory(_current);
            await Ui.Push(Navigation, new EditEntryPage(new PasswordEntry { Password = _current, Category = Categories.Logins }, isNew: true));
        };

        var defaults = Ui.SecondaryButton("Als Standard für neue Einträge");
        defaults.Clicked += (_, _) =>
        {
            MobileSettings.Length = (int)Math.Round(_length.Value);
            MobileSettings.Upper = _upper.IsToggled;
            MobileSettings.Lower = _lower.IsToggled;
            MobileSettings.Digits = _digits.IsToggled;
            MobileSettings.Symbols = _symbols.IsToggled;
            MobileSettings.ExcludeAmbiguous = _ambiguous.IsToggled;
            Ui.Toast(this, "Generator-Einstellungen gespeichert");
        };

        var display = Ui.Card(new VerticalStackLayout { Spacing = 12, Children = { _password, meter, _crackTime } }, new Thickness(16, 20, 16, 14));
        var buttons = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
            ColumnSpacing = 12,
            Children = { regenerate }
        };
        buttons.Add(copy, 1, 0);

        var options = Ui.Card(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
                    ColumnSpacing = 10,
                    MinimumHeightRequest = 44,
                    Children = { lengthLabel, Column(_length, 1) }
                },
                Ui.SettingRow("Großbuchstaben", "A–Z", _upper),
                Ui.SettingRow("Kleinbuchstaben", "a–z", _lower),
                Ui.SettingRow("Ziffern", "0–9", _digits),
                Ui.SettingRow("Sonderzeichen", "!@#$ …", _symbols),
                Ui.SettingRow("Verwechselbare meiden", "l, I, 1, O, 0 …", _ambiguous)
            }
        });

        var history = Ui.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children = { Ui.Caption("Verlauf (diese Sitzung)"), _historyEmpty, _history }
        });

        var views = new View[] { display, buttons, save, Ui.Section("Optionen"), options, defaults, Ui.Section("Verlauf"), history };
        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 8, 16, 40) };
        foreach (var v in views) stack.Add(v);
        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(views);

        _initializing = false;
        Generate();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderHistory();
    }

    private static View Column(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    private void Generate()
    {
        if (_initializing) return;
        // Mindestens eine Zeichengruppe muss aktiv bleiben
        if (!_upper.IsToggled && !_lower.IsToggled && !_digits.IsToggled && !_symbols.IsToggled)
        {
            _initializing = true;
            _lower.IsToggled = true;
            _initializing = false;
        }

        _current = _generator.Generate((int)Math.Round(_length.Value), _lower.IsToggled, _upper.IsToggled,
            _digits.IsToggled, _symbols.IsToggled, _ambiguous.IsToggled);

        var strength = PasswordStrength.Evaluate(_current);
        _updateMeter(_current);
        _crackTime.Text = $"Knackzeit: {PasswordStrength.EstimateCrackTime(strength.EntropyBits)}";
        _ = Scramble(_current);
    }

    // "Slot-Machine"-Effekt: Zeichen rasten nacheinander ein
    private async Task Scramble(string target)
    {
        if (Ui.ReduceMotion)
        {
            _password.FormattedText = Ui.ColoredPassword(target);
            return;
        }
        const string noise = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*?";
        var step = Math.Max(1, target.Length / 12);
        for (int fixedCount = 0; fixedCount < target.Length; fixedCount += step)
        {
            if (_current != target) return; // neueres Passwort übernimmt
            var chars = target.ToCharArray();
            for (int i = fixedCount; i < chars.Length; i++)
                chars[i] = noise[RandomNumberGenerator.GetInt32(noise.Length)];
            _password.FormattedText = null;
            _password.Text = new string(chars);
            await Task.Delay(28);
        }
        if (_current == target) _password.FormattedText = Ui.ColoredPassword(target);
    }

    private void Copy()
    {
        SecureClipboard.Copy(_current);
        AddToHistory(_current);
        RenderHistory();
        Ui.Toast(this, $"Kopiert – wird in {MobileSettings.ClipboardSeconds} s gelöscht");
    }

    private static void AddToHistory(string password)
    {
        History.Remove(password);
        History.Insert(0, password);
        while (History.Count > 10) History.RemoveAt(History.Count - 1);
    }

    private void RenderHistory()
    {
        _history.Clear();
        _historyEmpty.IsVisible = History.Count == 0;
        foreach (var pw in History)
        {
            var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
            row.Add(new Label { Text = pw, FontFamily = Ui.FontMono, FontSize = 14, TextColor = Ui.Muted, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center }, 0, 0);
            row.Add(Ui.IconButton(Ui.Icons.Copy, "Aus Verlauf kopieren", (_, _) =>
            {
                SecureClipboard.Copy(pw);
                Ui.Toast(this, "Kopiert");
            }), 1, 0);
            _history.Add(row);
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Linq;

namespace PasswordVault.App.Views;

/// <summary>
/// Gemeinsamer Dialog zum Anlegen und Bearbeiten eines Eintrags.
/// </summary>
public sealed partial class EntryEditorDialog : ContentDialog
{
    private readonly PasswordGenerator _generator = new();
    private readonly PasswordEntry _entry;
    private string _category;
    private bool _initializing = true;

    public PasswordEntry? Result { get; private set; }

    public EntryEditorDialog(PasswordEntry entry, bool isNew)
    {
        InitializeComponent();
        _entry = entry;
        _category = Categories.All.Contains(entry.Category) ? entry.Category : Categories.General;

        Title = isNew ? "Neuer Eintrag" : "Eintrag bearbeiten";
        PrimaryButtonText = isNew ? "Hinzufügen" : "Speichern";

        TitleBox.Text = entry.Title;
        UsernameBox.Text = entry.Username;
        PasswordInput.Password = entry.Password;
        UrlBox.Text = entry.Url;
        NotesBox.Text = entry.Notes;
        ExpiryBox.Text = entry.Expiry;
        CvcInput.Password = entry.Cvc;
        FavoriteToggle.IsOn = entry.IsFavorite;

        var s = App.Settings.Current;
        LengthSlider.Value = s.DefaultPasswordLength;
        UpperCheck.IsChecked = s.UseUppercase;
        LowerCheck.IsChecked = s.UseLowercase;
        DigitCheck.IsChecked = s.UseDigits;
        SymbolCheck.IsChecked = s.UseSymbols;
        AmbiguousCheck.IsChecked = s.ExcludeAmbiguous;
        LengthValue.Text = s.DefaultPasswordLength.ToString();

        UpdateCategoryButtons();
        UpdateAvatar();
        UpdateStrength();
        _initializing = false;

        PrimaryButtonClick += OnPrimaryButtonClick;
        Opened += (_, _) =>
        {
            Motion.Stagger(Form);
            Motion.PopIn(Avatar, 60);
            TitleBox.Focus(FocusState.Programmatic);
            // Neuer Login ohne Passwort → direkt einen Vorschlag machen
            if (isNew && string.IsNullOrEmpty(PasswordInput.Password) && _category is not (Categories.Notes or Categories.Cards))
                Generate();
        };
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            args.Cancel = true;
            ShowValidation("Bitte gib einen Titel ein.");
            TitleBox.Focus(FocusState.Programmatic);
            Motion.Shake(TitleBox);
            return;
        }

        var url = UrlBox.Text.Trim();
        if (url.Length > 0 && UrlMatcher.GetHost(url) == null)
        {
            args.Cancel = true;
            ShowValidation("Die Website-Adresse sieht ungültig aus (z.B. https://example.com).");
            Motion.Shake(UrlBox);
            return;
        }

        bool card = _category == Categories.Cards;
        var expiry = ExpiryBox.Text.Trim();
        if (card && expiry.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(expiry, @"^(0[1-9]|1[0-2])/\d{2}$"))
        {
            args.Cancel = true;
            ShowValidation("Ablaufdatum bitte als MM/JJ eingeben (z.B. 08/29).");
            Motion.Shake(ExpiryBox);
            return;
        }

        _entry.Title = TitleBox.Text.Trim();
        _entry.Username = UsernameBox.Text.Trim();
        // Leerzeichen aus der Kartennummer entfernen, damit Kopieren/Einfügen überall klappt
        _entry.Password = card ? PasswordInput.Password.Replace(" ", "") : PasswordInput.Password;
        _entry.Expiry = card ? expiry : "";
        _entry.Cvc = card ? CvcInput.Password.Trim() : "";
        _entry.Url = url;
        _entry.Notes = NotesBox.Text.Trim();
        _entry.Category = _category;
        _entry.IsFavorite = FavoriteToggle.IsOn;
        Result = _entry;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        if (ValidationPanel.Visibility != Visibility.Visible)
        {
            ValidationPanel.Visibility = Visibility.Visible;
            Motion.FadeInUp(ValidationPanel, 0, 8);
        }
    }

    private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var oldInitial = AvatarInitial.Text;
        UpdateAvatar();
        if (!_initializing && oldInitial != AvatarInitial.Text)
            Motion.Pulse(Avatar, 1.06f);
        if (ValidationPanel.Visibility == Visibility.Visible && TitleBox.Text.Length > 0)
            ValidationPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateAvatar()
    {
        Avatar.Background = Ui.AvatarBrush(TitleBox.Text);
        AvatarInitial.Text = Ui.Initial(TitleBox.Text);
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string tag)
        {
            _category = tag;
            UpdateCategoryButtons();
            Motion.Pulse(button, 1.06f);
        }
    }

    private void UpdateCategoryButtons()
    {
        foreach (var button in CategoryGrid.Children.OfType<ToggleButton>())
            button.IsChecked = button.Tag as string == _category;

        // Kreditkarte: eigenes Formular statt Passwort-Generator
        bool card = _category == Categories.Cards;
        UsernameBox.Header = card ? "Karteninhaber" : "Benutzername / E-Mail";
        UsernameBox.PlaceholderText = card ? "Max Mustermann" : "name@example.com";
        PasswordInput.Header = card ? "Kartennummer" : "Passwort";
        PasswordInput.PlaceholderText = card ? "1234 5678 9012 3456" : "Passwort eingeben oder generieren";
        var passwordOnly = card ? Visibility.Collapsed : Visibility.Visible;
        GenerateButton.Visibility = GeneratorOptions.Visibility = StrengthRow.Visibility = passwordOnly;
        var wasHidden = CardPanel.Visibility != Visibility.Visible;
        CardPanel.Visibility = card ? Visibility.Visible : Visibility.Collapsed;
        if (card && wasHidden && !_initializing) Motion.FadeInUp(CardPanel, 0, 8);
    }

    // "0829" → "08/29" beim Tippen
    private void ExpiryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = ExpiryBox.Text;
        if (text.Length == 3 && !text.Contains('/') && char.IsDigit(text[2]))
        {
            ExpiryBox.Text = text[..2] + "/" + text[2];
            ExpiryBox.SelectionStart = ExpiryBox.Text.Length;
        }
    }

    private void Password_Changed(object sender, RoutedEventArgs e) => UpdateStrength();

    private void UpdateStrength()
    {
        var result = PasswordStrength.Evaluate(PasswordInput.Password);
        StrengthFill.Background = Ui.StrengthBrush(result.Level);
        StrengthLabel.Text = result.Level == StrengthLevel.None
            ? "Kein Passwort"
            : $"{result.Label} · {result.EntropyBits:0} Bit";
        Motion.ScaleXTo(StrengthFill, Math.Max(result.Score, 2) / 100f);
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Generate();

    private void Generate()
    {
        bool upper = UpperCheck.IsChecked == true, lower = LowerCheck.IsChecked == true,
             digits = DigitCheck.IsChecked == true, symbols = SymbolCheck.IsChecked == true;
        if (!upper && !lower && !digits && !symbols)
        {
            LowerCheck.IsChecked = lower = true;
        }

        PasswordInput.Password = _generator.Generate(
            (int)LengthSlider.Value, lower, upper, digits, symbols,
            AmbiguousCheck.IsChecked == true);
        PasswordInput.PasswordRevealMode = PasswordRevealMode.Visible;
        // PasswordChanged feuert beim programmatischen Setzen nicht zuverlässig
        UpdateStrength();
        Motion.Spin(GenerateIcon);
        Motion.Pulse(PasswordInput, 1.02f);
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || LengthValue == null) return;
        LengthValue.Text = ((int)LengthSlider.Value).ToString();
        Generate();
    }
}

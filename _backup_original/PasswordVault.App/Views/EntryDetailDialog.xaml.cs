using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System.Linq;

namespace PasswordVault.App.Views;

public sealed partial class EntryDetailDialog : ContentDialog
{
    private readonly PasswordGenerator _generator = new();
    private readonly PasswordEntry _entry;

    public bool WasDeleted { get; private set; }

    public EntryDetailDialog(PasswordEntry entry)
    {
        InitializeComponent();
        _entry = entry;

        // Felder befüllen
        TitleBox.Text = entry.Title;
        UsernameBox.Text = entry.Username;
        PasswordBox.Password = entry.Password;
        UrlBox.Text = entry.Url;
        NotesBox.Text = entry.Notes;
        FavoriteToggle.IsOn = entry.IsFavorite;

        // Kategorie setzen
        for (int i = 0; i < CategoryBox.Items.Count; i++)
        {
            var item = CategoryBox.Items[i] as ComboBoxItem;
            if (item?.Content?.ToString() == entry.Category)
            {
                CategoryBox.SelectedIndex = i;
                break;
            }
        }

        // Passwort-Stärke anzeigen
        UpdateStrength(entry.Password);

        // Events
        PasswordBox.PasswordChanged += (s, e) =>
            UpdateStrength(PasswordBox.Password);
        PrimaryButtonClick += OnSave;
        SecondaryButtonClick += OnDelete;
    }

    private void OnSave(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        _entry.Title = TitleBox.Text.Trim();
        _entry.Username = UsernameBox.Text.Trim();
        _entry.Password = PasswordBox.Password;
        _entry.Url = UrlBox.Text.Trim();
        _entry.Notes = NotesBox.Text.Trim();
        _entry.IsFavorite = FavoriteToggle.IsOn;
        _entry.Category = (CategoryBox.SelectedItem
            as ComboBoxItem)?.Content?.ToString()
            ?? "Allgemein";
        _entry.ModifiedAt = System.DateTime.UtcNow;
    }

    private void OnDelete(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        WasDeleted = true;
    }

    private void CopyPassword_Click(
        object sender, RoutedEventArgs e)
    {
        ClipboardHelper.CopyWithAutoClear(
            PasswordBox.Password, DispatcherQueue);
    }

    private void GeneratePassword_Click(
        object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = _generator.Generate(
            length: 20, useSymbols: true);
    }

    private void UpdateStrength(string password)
    {
        var score = CalculateStrength(password);
        StrengthBar.Value = score;

        (StrengthText.Text, StrengthBar.Foreground) = score switch
        {
            < 25 => ("Sehr schwach",
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 220, 38, 38))),
            < 50 => ("Schwach",
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 245, 158, 11))),
            < 75 => ("Mittel",
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 59, 130, 246))),
            _ => ("Stark",
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 34, 197, 94)))
        };
    }

    private static int CalculateStrength(string pw)
    {
        if (string.IsNullOrEmpty(pw)) return 0;
        int score = 0;
        if (pw.Length >= 8) score += 20;
        if (pw.Length >= 12) score += 10;
        if (pw.Length >= 16) score += 10;
        if (pw.Any(char.IsUpper)) score += 15;
        if (pw.Any(char.IsLower)) score += 15;
        if (pw.Any(char.IsDigit)) score += 15;
        if (pw.Any(c => !char.IsLetterOrDigit(c)))
            score += 15;
        return System.Math.Min(100, score);
    }
}

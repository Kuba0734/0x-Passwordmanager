using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.App.Views;

public sealed partial class AddEntryDialog : ContentDialog
{
    private readonly PasswordGenerator _generator = new();

    public PasswordEntry? Result { get; private set; }

    public AddEntryDialog()
    {
        InitializeComponent();

        // Validierung beim Speichern
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            args.Cancel = true;
            TitleBox.Focus(FocusState.Programmatic);
            return;
        }

        Result = new PasswordEntry
        {
            Title = TitleBox.Text.Trim(),
            Username = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password,
            Url = UrlBox.Text.Trim(),
            Notes = NotesBox.Text.Trim(),
            Category = (CategoryBox.SelectedItem
                as ComboBoxItem)?.Content?.ToString()
                ?? "Allgemein"
        };
    }

    private void GeneratePassword_Click(
        object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = _generator.Generate(
            length: 20,
            useSymbols: true);
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PasswordVault.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PasswordVault.App.Views;

public sealed partial class MainPage : Page
{
    public ObservableCollection<PasswordEntry> Entries { get; }
        = new();

    private List<PasswordEntry> _allEntries = new();
    private string _currentFilter = "all";

    public MainPage()
    {
        InitializeComponent();
        EntryListView.ItemsSource = Entries;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (App.CurrentEntries != null)
        {
            _allEntries = App.CurrentEntries;
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        var filtered = _currentFilter switch
        {
            "favorites" => _allEntries
                .Where(x => x.IsFavorite),
            "logins" => _allEntries
                .Where(x => x.Category == "Logins"),
            "cards" => _allEntries
                .Where(x => x.Category == "Kreditkarten"),
            "notes" => _allEntries
                .Where(x => x.Category == "Notizen"),
            _ => _allEntries.AsEnumerable()
        };

        // Alphabetisch sortieren, Favoriten zuerst
        foreach (var entry in filtered
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Title))
        {
            Entries.Add(entry);
        }

        UpdateEmptyState();

        // Zähler aktualisieren
        PageTitle.Text = _currentFilter switch
        {
            "all" => $"Alle Einträge ({_allEntries.Count})",
            "favorites" => $"Favoriten ({Entries.Count})",
            "logins" => $"Logins ({Entries.Count})",
            "cards" => $"Kreditkarten ({Entries.Count})",
            "notes" => $"Notizen ({Entries.Count})",
            _ => $"Alle Einträge ({_allEntries.Count})"
        };
    }


    /// <summary>
    /// Verschlüsselt und speichert alle Einträge.
    /// </summary>
    private void SaveVault()
    {

        var encrypted = App.Vault.EncryptEntries(_allEntries);
        var metadata = new VaultMetadata
        {
            Salt = App.Vault.GetSalt(),
            LastOpenedAt = System.DateTime.UtcNow
        };
        App.VaultFile.Save(App.VaultPath, metadata, encrypted);
        System.Diagnostics.Debug.WriteLine("File exists after save: " +
            System.IO.File.Exists(App.VaultPath));
    }


    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentArea.Visibility = Visibility.Collapsed;
            SettingsFrame.Visibility = Visibility.Visible;
            SettingsFrame.Navigate(typeof(SettingsPage));
            return;
        }

        ContentArea.Visibility = Visibility.Visible;
        SettingsFrame.Visibility = Visibility.Collapsed;

        var item = args.SelectedItem as NavigationViewItem;
        var tag = item?.Tag?.ToString() ?? "all";
        _currentFilter = tag;

        PageTitle.Text = tag switch
        {
            "all" => "Alle Einträge",
            "favorites" => "Favoriten",
            "logins" => "Logins",
            "cards" => "Kreditkarten",
            "notes" => "Notizen",
            _ => "Alle Einträge"
        };

        ApplyFilter();
    }

    private async void AddEntry_Click(
        object sender, RoutedEventArgs e)
    {
        var dialog = new AddEntryDialog
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary
            && dialog.Result != null)
        {
            _allEntries.Add(dialog.Result);
            ApplyFilter();
            SaveVault();  // Sofort speichern!
        }
    }

    private async void EntryList_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (EntryListView.SelectedItem
            is not PasswordEntry selected) return;

        var dialog = new EntryDetailDialog(selected)
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (dialog.WasDeleted)
        {
            _allEntries.Remove(selected);
        }

        // In jedem Fall neu filtern und speichern
        ApplyFilter();
        SaveVault();

        // Auswahl zurücksetzen
        EntryListView.SelectedItem = null;
    }

    private void SearchBox_TextChanged(
    AutoSuggestBox sender,
    AutoSuggestBoxTextChangedEventArgs args)
    {
        var query = sender.Text.Trim().ToLower();

        Entries.Clear();

        var results = string.IsNullOrEmpty(query)
            ? _allEntries
            : _allEntries.Where(e =>
                e.Title.ToLower().Contains(query) ||
                e.Username.ToLower().Contains(query) ||
                e.Url.ToLower().Contains(query));

        foreach (var entry in results)
            Entries.Add(entry);

        UpdateEmptyState();
    }



    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Entries.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }
}

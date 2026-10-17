using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PasswordVault.App.Views;

public sealed partial class VaultPage : Page
{
    private readonly ObservableCollection<PasswordEntry> _items = new();
    private List<PasswordEntry> _all = new();
    private string _filter = "all";
    private string _sort = "name";
    private PasswordEntry? _selected;
    private string? _pendingSelectId;
    private bool _startNewOnLoad;
    private bool _suppressSelection;
    private bool _revealed;
    private bool _narrow;

    private static readonly Dictionary<string, (string Title, string Glyph)> Filters = new()
    {
        ["all"] = ("Alle Einträge", ""),
        ["favorites"] = ("Favoriten", ""),
        ["logins"] = ("Logins", ""),
        ["cards"] = ("Kreditkarten", ""),
        ["notes"] = ("Notizen", ""),
        ["general"] = ("Allgemein", ""),
    };

    public VaultPage()
    {
        InitializeComponent();
        EntryList.ItemsSource = _items;
        Loaded += OnLoaded;
        Unloaded += (_, _) => App.Store.Changed -= Store_Changed;
        SizeChanged += (_, _) => ApplyLayout();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is VaultNavigation nav)
        {
            _filter = Filters.ContainsKey(nav.Filter) ? nav.Filter : "all";
            _pendingSelectId = nav.SelectId;
            _startNewOnLoad = nav.StartNew;
        }
        UpdateHeader(animate: false);
        Reload();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Store.Changed += Store_Changed;
        ApplyLayout();

        Motion.FadeInUp(Header, 0);
        Motion.FadeInUp(Toolbar, Motion.StaggerDelay(1));
        if (!_narrow) Motion.FadeInUp(DetailCard, Motion.StaggerDelay(2));

        if (_startNewOnLoad)
        {
            _startNewOnLoad = false;
            StartNewEntry();
        }
    }

    private void Store_Changed(object? sender, VaultChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!App.Store.IsUnlocked) return;
            if (e.Source == VaultChangeSource.Browser)
                App.MainWindowInstance.ShowToast("Tresor über die Browser-Erweiterung aktualisiert", "");
            Reload();
        });
    }

    // ================= Daten & Filter =================

    private void Reload()
    {
        if (!App.Store.IsUnlocked) return;
        _all = App.Store.GetEntries();
        ApplyFilter();
    }

    public void SetFilter(string tag)
    {
        if (!Filters.ContainsKey(tag)) tag = "all";
        if (_filter == tag) return;
        _filter = tag;
        UpdateHeader(animate: true);
        ApplyFilter();
    }

    public void FocusSearch() => SearchBox.Focus(FocusState.Keyboard);

    private void UpdateHeader(bool animate)
    {
        var (title, glyph) = Filters[_filter];
        PageTitle.Text = title;
        HeaderIcon.Glyph = glyph;
        if (animate)
        {
            Motion.PopIn(HeaderIcon);
            Motion.Crossfade(PageTitle);
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<PasswordEntry> query = _filter switch
        {
            "favorites" => _all.Where(x => x.IsFavorite),
            "logins" => _all.Where(x => x.Category == Categories.Logins),
            "cards" => _all.Where(x => x.Category == Categories.Cards),
            "notes" => _all.Where(x => x.Category == Categories.Notes),
            "general" => _all.Where(x => x.Category == Categories.General),
            _ => _all
        };

        var search = SearchBox.Text.Trim();
        if (search.Length > 0)
            query = query.Where(e =>
                e.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                e.Username.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                e.Url.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                e.Notes.Contains(search, StringComparison.CurrentCultureIgnoreCase));

        query = _sort switch
        {
            "modified" => query.OrderByDescending(e => e.ModifiedAt),
            "created" => query.OrderByDescending(e => e.CreatedAt),
            _ => query.OrderByDescending(e => e.IsFavorite)
                      .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
        };

        var result = query.ToList();
        var keepId = _pendingSelectId ?? _selected?.Id;

        _suppressSelection = true;
        SyncItems(result);
        var toSelect = keepId == null ? null : _items.FirstOrDefault(i => i.Id == keepId);
        EntryList.SelectedItem = toSelect;
        _suppressSelection = false;

        if (toSelect != null)
        {
            EntryList.ScrollIntoView(toSelect);
            bool isNew = _pendingSelectId != null || _selected == null || _selected.Id != toSelect.Id;
            _selected = toSelect;
            ShowDetail(animate: isNew);
        }
        else if (_selected != null || DetailScroller.Visibility == Visibility.Visible)
        {
            // Ausgewählter Eintrag ist weg (gelöscht oder weggefiltert)
            _selected = null;
            ShowDetail(animate: false);
        }
        _pendingSelectId = null;

        // Untertitel
        var favs = _all.Count(e => e.IsFavorite);
        PageSubtitle.Text = search.Length > 0
            ? $"{result.Count} Treffer für „{search}“"
            : $"{result.Count} {(result.Count == 1 ? "Eintrag" : "Einträge")}" +
              (_filter == "all" && favs > 0 ? $" · {favs} Favoriten" : "");

        UpdateEmptyState(search);
    }

    /// <summary>
    /// Gleicht die sichtbare Liste minimal ab, damit nur echte Änderungen animiert werden
    /// (Einfügen, Entfernen, Verschieben) statt die ganze Liste neu aufzubauen.
    /// </summary>
    private void SyncItems(List<PasswordEntry> target)
    {
        var targetIds = target.Select(t => t.Id).ToHashSet();
        for (int i = _items.Count - 1; i >= 0; i--)
            if (!targetIds.Contains(_items[i].Id))
                _items.RemoveAt(i);

        for (int i = 0; i < target.Count; i++)
        {
            var t = target[i];
            if (i < _items.Count && _items[i].Id == t.Id)
            {
                if (_items[i].ModifiedAt != t.ModifiedAt) _items[i] = t;
                continue;
            }

            int existing = -1;
            for (int j = i + 1; j < _items.Count; j++)
                if (_items[j].Id == t.Id) { existing = j; break; }

            if (existing >= 0)
            {
                _items.Move(existing, i);
                if (_items[i].ModifiedAt != t.ModifiedAt) _items[i] = t;
            }
            else
            {
                _items.Insert(i, t);
            }
        }
    }

    private void UpdateEmptyState(string search)
    {
        bool empty = _items.Count == 0;
        bool wasVisible = EmptyState.Visibility == Visibility.Visible;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty) return;

        if (search.Length > 0)
        {
            EmptyIcon.Glyph = "";
            EmptyTitle.Text = "Keine Treffer";
            EmptyText.Text = $"Für „{search}“ wurde nichts gefunden. Versuche einen anderen Suchbegriff.";
            EmptyAddButton.Visibility = Visibility.Collapsed;
        }
        else if (_all.Count == 0)
        {
            EmptyIcon.Glyph = "";
            EmptyTitle.Text = "Noch keine Einträge";
            EmptyText.Text = "Füge deinen ersten Zugang hinzu – er wird sofort verschlüsselt gespeichert.";
            EmptyAddButton.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyIcon.Glyph = Filters[_filter].Glyph;
            EmptyTitle.Text = "Hier ist noch nichts";
            EmptyText.Text = _filter == "favorites"
                ? "Markiere Einträge mit dem Stern, um sie hier schnell wiederzufinden."
                : "In dieser Kategorie gibt es noch keine Einträge.";
            EmptyAddButton.Visibility = _filter == "favorites" ? Visibility.Collapsed : Visibility.Visible;
        }

        if (!wasVisible)
        {
            Motion.PopIn(EmptyIllustration);
            Motion.FadeInUp(EmptyTitle, Motion.StaggerDelay(1));
            Motion.FadeInUp(EmptyText, Motion.StaggerDelay(2));
            Motion.FadeInUp(EmptyAddButton, Motion.StaggerDelay(3));
        }
    }

    // ================= Auswahl & Details =================

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        _selected = EntryList.SelectedItem as PasswordEntry;
        ShowDetail(animate: true);
    }

    private void ShowDetail(bool animate)
    {
        var entry = _selected;
        _revealed = false;

        if (entry == null)
        {
            DetailScroller.Visibility = Visibility.Collapsed;
            DetailPlaceholder.Visibility = Visibility.Visible;
            if (_narrow) SetNarrowDetailOpen(false);
            return;
        }

        DetailPlaceholder.Visibility = Visibility.Collapsed;
        DetailScroller.Visibility = Visibility.Visible;

        DetailAvatar.Background = Ui.AvatarBrush(entry.Title);
        DetailInitial.Text = Ui.Initial(entry.Title);
        DetailTitle.Text = entry.Title;
        DetailHost.Text = Ui.Host(entry.Url);
        DetailHost.Visibility = Ui.VisibleIf(DetailHost.Text);

        FavoriteIcon.Glyph = entry.IsFavorite ? "" : "";
        if (entry.IsFavorite)
            FavoriteIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FavoriteBrush"];
        else
            FavoriteIcon.ClearValue(FontIcon.ForegroundProperty);

        UserField.Visibility = Ui.VisibleIf(entry.Username);
        DetailUser.Text = entry.Username;

        PasswordField.Visibility = Ui.VisibleIf(entry.Password);
        StrengthPanel.Visibility = PasswordField.Visibility;
        SetPasswordMasked();
        RevealIcon.Glyph = "";

        var strength = PasswordStrength.Evaluate(entry.Password);
        StrengthLabel.Text = $"Passwortstärke: {strength.Label}";
        StrengthDetail.Text = $"≈ {strength.EntropyBits:0} Bit · {entry.Password.Length} Zeichen";
        StrengthFill.Background = Ui.StrengthBrush(strength.Level);
        Motion.ScaleXTo(StrengthFill, Math.Max(strength.Score, 3) / 100f, animate ? Motion.Slow : Motion.Fast);

        UrlField.Visibility = Ui.VisibleIf(entry.Url);
        DetailUrl.Text = entry.Url;

        NotesField.Visibility = Ui.VisibleIf(entry.Notes);
        DetailNotes.Text = entry.Notes;

        DetailCategory.Text = entry.Category;
        DetailCategoryIcon.Glyph = Ui.CategoryGlyph(entry.Category);
        DetailMeta.Text = $"Geändert {Ui.RelativeTime(entry.ModifiedAt)} · erstellt {entry.CreatedAt.ToLocalTime():dd.MM.yyyy}";

        if (_narrow) SetNarrowDetailOpen(true);

        if (animate)
        {
            DetailScroller.ChangeView(null, 0, null, true);
            Motion.Stagger(DetailPanel);
            Motion.PopIn(DetailAvatar, 40);
        }
    }

    private void SetPasswordMasked()
    {
        var length = Math.Clamp(_selected?.Password.Length ?? 0, 8, 18);
        DetailPassword.Inlines.Clear();
        DetailPassword.Text = new string('•', length);
    }

    // ================= Layout (breit / schmal) =================

    private void ApplyLayout()
    {
        bool narrow = ActualWidth > 0 && ActualWidth < 880;
        if (narrow == _narrow && ActualWidth > 0)
        {
            if (!narrow) DetailColumn.Width = new GridLength(ActualWidth < 1150 ? 360 : 420);
            return;
        }
        _narrow = narrow;

        if (narrow)
        {
            DetailColumn.Width = new GridLength(0);
            LayoutRoot.ColumnSpacing = 0;
            Grid.SetColumn(DetailCard, 0);
            DetailBackButton.Visibility = Visibility.Visible;
            SetNarrowDetailOpen(_selected != null);
        }
        else
        {
            DetailColumn.Width = new GridLength(ActualWidth < 1150 ? 360 : 420);
            LayoutRoot.ColumnSpacing = 24;
            Grid.SetColumn(DetailCard, 1);
            DetailBackButton.Visibility = Visibility.Collapsed;
            DetailCard.Visibility = Visibility.Visible;
            EntryList.Visibility = Visibility.Visible;
            Toolbar.Visibility = Visibility.Visible;
            UpdateEmptyState(SearchBox.Text.Trim());
        }
    }

    private void SetNarrowDetailOpen(bool open)
    {
        DetailCard.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        EntryList.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        Toolbar.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        if (open)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            Motion.Crossfade(DetailCard);
        }
        else
        {
            UpdateEmptyState(SearchBox.Text.Trim());
        }
    }

    private void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        _suppressSelection = true;
        EntryList.SelectedItem = null;
        _suppressSelection = false;
        _selected = null;
        SetNarrowDetailOpen(false);
    }

    // ================= Suche & Sortierung =================

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter();
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _sort = (SortBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "name";
        if (IsLoaded) ApplyFilter();
    }

    // ================= Aktionen =================

    private void AddEntry_Click(object sender, RoutedEventArgs e) => StartNewEntry();

    public async void StartNewEntry()
    {
        var preset = new PasswordEntry
        {
            Category = _filter switch
            {
                "cards" => Categories.Cards,
                "notes" => Categories.Notes,
                "general" => Categories.General,
                _ => Categories.Logins
            },
            IsFavorite = _filter == "favorites"
        };

        var dialog = new EntryEditorDialog(preset, isNew: true);
        if (await App.MainWindowInstance.ShowDialogAsync(dialog) != ContentDialogResult.Primary
            || dialog.Result == null)
            return;

        try
        {
            App.Store.Add(dialog.Result);
            _pendingSelectId = dialog.Result.Id;
            if (_filter != "all" && !Matches(dialog.Result))
                SetFilter("all");
            App.MainWindowInstance.ShowToast($"„{dialog.Result.Title}“ wurde gespeichert", "");
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Speichern fehlgeschlagen", ex.Message);
        }
    }

    private bool Matches(PasswordEntry e) => _filter switch
    {
        "favorites" => e.IsFavorite,
        "logins" => e.Category == Categories.Logins,
        "cards" => e.Category == Categories.Cards,
        "notes" => e.Category == Categories.Notes,
        "general" => e.Category == Categories.General,
        _ => true
    };

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var dialog = new EntryEditorDialog(_selected.Clone(), isNew: false);
        if (await App.MainWindowInstance.ShowDialogAsync(dialog) != ContentDialogResult.Primary
            || dialog.Result == null)
            return;

        try
        {
            App.Store.Update(dialog.Result);
            _pendingSelectId = dialog.Result.Id;
            App.MainWindowInstance.ShowToast("Änderungen gespeichert", "");
        }
        catch (Exception ex)
        {
            await App.MainWindowInstance.ShowMessageAsync("Speichern fehlgeschlagen", ex.Message);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var entry = _selected;
        var confirmed = await App.MainWindowInstance.ConfirmAsync(
            "Eintrag löschen?",
            $"„{entry.Title}“ wird endgültig aus dem Tresor entfernt. Das kann nicht rückgängig gemacht werden.",
            "Löschen", danger: true);
        if (!confirmed) return;

        App.Store.Delete(entry.Id);
        App.MainWindowInstance.ShowToast($"„{entry.Title}“ wurde gelöscht", "");
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var updated = _selected.Clone();
        updated.IsFavorite = !updated.IsFavorite;
        App.Store.Update(updated);
        _pendingSelectId = updated.Id;
        Motion.Pulse(FavoriteButton, 1.15f);
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _revealed = !_revealed;
        if (_revealed)
        {
            Ui.SetColoredPassword(DetailPassword, _selected.Password);
            RevealIcon.Glyph = "";
        }
        else
        {
            SetPasswordMasked();
            RevealIcon.Glyph = "";
        }
        Motion.Crossfade(DetailPassword, Motion.Fast);
    }

    private void CopyUser_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ClipboardHelper.CopyPlain(_selected.Username, "Benutzername");
        Motion.Pulse((UIElement)sender);
    }

    private void CopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ClipboardHelper.CopySecret(_selected.Password);
        Motion.Pulse((UIElement)sender);
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ClipboardHelper.CopyPlain(_selected.Url, "Website");
        Motion.Pulse((UIElement)sender);
    }

    private async void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var url = _selected.Url.Trim();
        if (!url.Contains("://")) url = "https://" + url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            await Windows.System.Launcher.LaunchUriAsync(uri);
        else
            App.MainWindowInstance.ShowToast("Ungültige Website-Adresse", "");
    }

    private void QuickCopyUser_Click(object sender, RoutedEventArgs e)
    {
        var entry = _all.FirstOrDefault(x => x.Id == (sender as Button)?.Tag as string);
        if (entry == null) return;
        ClipboardHelper.CopyPlain(entry.Username, "Benutzername");
        Motion.Pulse((UIElement)sender);
    }

    private void QuickCopyPassword_Click(object sender, RoutedEventArgs e)
    {
        var entry = _all.FirstOrDefault(x => x.Id == (sender as Button)?.Tag as string);
        if (entry == null) return;
        ClipboardHelper.CopySecret(entry.Password);
        Motion.Pulse((UIElement)sender);
    }
}

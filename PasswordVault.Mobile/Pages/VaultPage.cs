using Microsoft.Maui.Controls.Shapes;
using PasswordVault.Core.Models;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Liste aller Einträge mit Suche, Filtern und Sortierung. Wischen nach links kopiert das Passwort.
/// </summary>
public class VaultPage : ContentPage
{
    private const string All = "Alle";
    private const string Favorites = "Favoriten";

    private readonly SearchBar _search = new()
    {
        Placeholder = "Suchen",
        TextColor = Ui.Text,
        PlaceholderColor = Ui.Muted,
        CancelButtonColor = Ui.Accent,
        FontFamily = Ui.Font,
        BackgroundColor = Colors.Transparent
    };
    private readonly HorizontalStackLayout _chips = new() { Spacing = 8, Padding = new Thickness(16, 0) };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Label _count = new() { TextColor = Ui.Muted, FontSize = 13, Margin = new Thickness(20, 4, 20, 0) };
    private readonly Label _emptyTitle = new() { FontSize = 18, FontFamily = Ui.FontBold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _emptyText = new() { TextColor = Ui.Muted, HorizontalTextAlignment = TextAlignment.Center };
    private List<PasswordEntry> _entries = new();
    private string _filter = All;

    private static readonly (string Label, string Key)[] Sorts = { ("Name", "name"), ("Zuletzt geändert", "modified"), ("Zuletzt erstellt", "created") };
    private static string SortKey
    {
        get => Preferences.Default.Get("sort", "name");
        set => Preferences.Default.Set("sort", value);
    }

    public VaultPage()
    {
        Title = "Tresor";
        ToolbarItems.Add(new ToolbarItem("Sperren", null, App.ShowUnlock));
        ToolbarItems.Add(new ToolbarItem("Sortieren", null, async () => await ChooseSortAsync()));

        foreach (var name in new[] { All, Favorites }.Concat(Categories.All))
            _chips.Add(CreateChip(name));
        UpdateChips();

        _search.TextChanged += (_, _) => Refresh();
        _list.ItemTemplate = new DataTemplate(() => new EntryRow(this));
        _list.EmptyView = new VerticalStackLayout
        {
            Padding = new Thickness(40, 80),
            Spacing = 8,
            Children =
            {
                Ui.Icon(Ui.Icons.Key, 44, Ui.Muted),
                _emptyTitle,
                _emptyText
            }
        };

        var add = new Border
        {
            BackgroundColor = Ui.Accent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 32 },
            WidthRequest = 64,
            HeightRequest = 64,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 22, 30),
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Radius = 24, Opacity = 0.6f, Offset = new Point(0, 8) },
            Content = Ui.Icon(Ui.Icons.Plus, 28, Ui.OnAccent)
        };
        SemanticProperties.SetDescription(add, "Neuer Eintrag");
        var addTap = new TapGestureRecognizer();
        addTap.Tapped += async (_, _) =>
        {
            await add.ScaleToAsync(0.97, Ui.Fast, Ui.Ease);
            await add.ScaleToAsync(1, Ui.Normal, Ui.Ease);
            await Ui.Push(Navigation, new EditEntryPage(NewEntryPreset(), isNew: true));
        };
        add.GestureRecognizers.Add(addTap);

        var layout = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) }
        };
        layout.Add(_search, 0, 0);
        layout.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _chips }, 0, 1);
        layout.Add(_count, 0, 2);
        layout.Add(_list, 0, 3);
        layout.Add(add, 0, 3);

        Content = Ui.PageRoot(layout);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!App.Store.IsUnlocked) return;
        _entries = App.Store.GetEntries();
        Refresh();
    }

    private void Refresh()
    {
        var q = _search.Text?.Trim() ?? "";
        var filtered = _entries
            .Where(e => _filter switch
            {
                All => true,
                Favorites => e.IsFavorite,
                _ => e.Category == _filter
            })
            .Where(e => q.Length == 0
                || e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || e.Username.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || e.Url.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || e.Notes.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        var items = (SortKey switch
        {
            "modified" => filtered.OrderByDescending(e => e.ModifiedAt),
            "created" => filtered.OrderByDescending(e => e.CreatedAt),
            _ => filtered.OrderByDescending(e => e.IsFavorite).ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
        }).ToList();
        _list.ItemsSource = items;

        var favorites = _entries.Count(e => e.IsFavorite);
        _count.Text = q.Length > 0
            ? $"{items.Count} Treffer für „{q}“"
            : (items.Count == 1 ? "1 Eintrag" : $"{items.Count} Einträge") + (_filter == All && favorites > 0 ? $" · {favorites} Favoriten" : "");

        (_emptyTitle.Text, _emptyText.Text) = q.Length > 0
            ? ("Keine Treffer", $"Für „{q}“ wurde nichts gefunden.")
            : _entries.Count == 0
                ? ("Noch keine Einträge", "Tippe auf + um deinen ersten Zugang hinzuzufügen – er wird sofort verschlüsselt gespeichert.")
                : _filter == Favorites
                    ? ("Hier ist noch nichts", "Markiere Einträge als Favorit, um sie hier schnell wiederzufinden.")
                    : ("Hier ist noch nichts", "In dieser Kategorie gibt es noch keine Einträge.");
    }

    private PasswordEntry NewEntryPreset() => new()
    {
        Category = Categories.All.Contains(_filter) ? _filter : Categories.Logins,
        IsFavorite = _filter == Favorites
    };

    private async Task ChooseSortAsync()
    {
        var choice = await DisplayActionSheetAsync("Sortieren nach", "Abbrechen", null,
            Sorts.Select(s => (s.Key == SortKey ? "✓ " : "") + s.Label).ToArray());
        var match = Sorts.FirstOrDefault(s => choice != null && choice.EndsWith(s.Label));
        if (match.Key == null) return;
        SortKey = match.Key;
        Refresh();
    }

    private Button CreateChip(string name)
    {
        var chip = Ui.Pressable(new Button { Text = name, FontSize = 14, CornerRadius = 17, HeightRequest = 34, Padding = new Thickness(14, 0), BorderWidth = 1 });
        chip.Clicked += (_, _) =>
        {
            _filter = name;
            UpdateChips();
            Refresh();
        };
        return chip;
    }

    private void UpdateChips()
    {
        foreach (var chip in _chips.Children.OfType<Button>())
        {
            var active = chip.Text == _filter;
            chip.BackgroundColor = active ? Ui.Accent : Ui.Surface;
            chip.BorderColor = active ? Ui.Accent : Ui.Stroke;
            chip.TextColor = active ? Ui.OnAccent : Ui.Muted;
        }
    }

    internal async Task OpenAsync(PasswordEntry entry) => await Ui.Push(Navigation, new EntryPage(entry.Id));

    internal int IndexOf(PasswordEntry entry) => (_list.ItemsSource as List<PasswordEntry>)?.IndexOf(entry) ?? 0;

    internal void CopyPassword(PasswordEntry entry)
    {
        SecureClipboard.Copy(entry.Password);
        Ui.Toast(this, "Passwort kopiert");
    }

    /// <summary>Eine Zeile der Liste; wird aus dem Eintrag gebaut, wenn die Liste ihn zuweist.</summary>
    private sealed class EntryRow : ContentView
    {
        private readonly VaultPage _page;

        public EntryRow(VaultPage page) => _page = page;

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not PasswordEntry entry) return;

            var text = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = entry.Title, FontSize = 17, FontFamily = Ui.FontBold, LineBreakMode = LineBreakMode.TailTruncation },
                    new Label { Text = string.IsNullOrEmpty(entry.Username) ? entry.Category : entry.Username, FontSize = 14, TextColor = Ui.Muted, LineBreakMode = LineBreakMode.TailTruncation }
                }
            };
            var row = new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
                ColumnSpacing = 14
            };
            row.Add(Ui.Avatar(entry.Title), 0, 0);
            row.Add(text, 1, 0);
            if (entry.IsFavorite)
                row.Add(Ui.Icon(Ui.Icons.Star, 16, Ui.Muted, filled: true), 2, 0);

            var card = Ui.Card(row, new Thickness(12));
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await card.ScaleToAsync(0.97, Ui.Fast, Ui.Ease);
                    await card.ScaleToAsync(1, Ui.Normal, Ui.Ease);
                    await _page.OpenAsync(entry);
                })
            });

            var copy = new SwipeItemView
            {
                Content = new Border
                {
                    BackgroundColor = Ui.SurfaceStrong,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 18 },
                    WidthRequest = 110,
                    Margin = new Thickness(6, 0, 0, 0),
                    Content = new Label { Text = "Passwort\nkopieren", TextColor = Colors.White, FontSize = 13, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center }
                }
            };
            copy.Invoked += (_, _) => _page.CopyPassword(entry);

            Content = new SwipeView
            {
                Margin = new Thickness(16, 5),
                RightItems = new SwipeItems { copy },
                Content = card
            };
            SemanticProperties.SetDescription(card, $"{entry.Title}, {entry.Username}");

            // gestaffelt: 45 ms pro Zeile, ab der 10. gemeinsam
            if (!Ui.ReduceMotion) _ = Ui.FadeUp(this, (uint)(Math.Min(_page.IndexOf(entry), 10) * Ui.StaggerStep));
        }
    }
}

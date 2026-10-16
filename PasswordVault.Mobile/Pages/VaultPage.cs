using Microsoft.Maui.Controls.Shapes;
using PasswordVault.Core.Models;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Liste aller Einträge mit Suche und Filtern. Wischen nach links kopiert das Passwort.
/// </summary>
public class VaultPage : ContentPage
{
    private const string All = "Alle";
    private const string Favorites = "★ Favoriten";

    private readonly SearchBar _search = new()
    {
        Placeholder = "Suchen",
        TextColor = Ui.Text,
        PlaceholderColor = Ui.Muted,
        CancelButtonColor = Ui.Violet,
        BackgroundColor = Colors.Transparent
    };
    private readonly HorizontalStackLayout _chips = new() { Spacing = 8, Padding = new Thickness(16, 0) };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Label _count = new() { TextColor = Ui.Muted, FontSize = 13, Margin = new Thickness(20, 4, 20, 0) };
    private List<PasswordEntry> _entries = new();
    private string _filter = All;

    public VaultPage()
    {
        Title = "Tresor";
        ToolbarItems.Add(new ToolbarItem("Sperren", null, App.ShowUnlock));
        ToolbarItems.Add(new ToolbarItem("Einstellungen", null, async () => await Navigation.PushAsync(new SettingsPage())));

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
                new Label { Text = "🗝️", FontSize = 48, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "Keine Einträge", FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "Tippe auf + um ein Passwort hinzuzufügen.", TextColor = Ui.Muted, HorizontalTextAlignment = TextAlignment.Center }
            }
        };

        var add = new Button
        {
            Text = "+",
            FontSize = 32,
            Padding = 0,
            TextColor = Colors.White,
            Background = Ui.AccentGradient,
            CornerRadius = 32,
            WidthRequest = 64,
            HeightRequest = 64,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 22, 30),
            Shadow = new Shadow { Brush = new SolidColorBrush(Ui.Violet), Radius = 24, Opacity = 0.6f, Offset = new Point(0, 8) }
        };
        SemanticProperties.SetDescription(add, "Neuer Eintrag");
        add.Clicked += async (_, _) =>
        {
            await add.ScaleToAsync(0.88, 70);
            await add.ScaleToAsync(1, 120, Easing.SpringOut);
            await Navigation.PushAsync(new EditEntryPage(null));
        };

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
        var items = _entries
            .Where(e => _filter switch
            {
                All => true,
                Favorites => e.IsFavorite,
                _ => e.Category == _filter
            })
            .Where(e => q.Length == 0
                || e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || e.Username.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || e.Url.Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _list.ItemsSource = items;
        _count.Text = items.Count == 1 ? "1 Eintrag" : $"{items.Count} Einträge";
    }

    private Button CreateChip(string name)
    {
        var chip = new Button { Text = name, FontSize = 14, CornerRadius = 16, HeightRequest = 34, Padding = new Thickness(14, 0) };
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
            chip.Background = active ? Ui.AccentGradient : new SolidColorBrush(Ui.Surface2);
            chip.TextColor = active ? Colors.White : Ui.Muted;
        }
    }

    internal async Task OpenAsync(PasswordEntry entry) => await Navigation.PushAsync(new EntryPage(entry.Id));

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
                    new Label { Text = entry.Title, FontSize = 17, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation },
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
                row.Add(new Label { Text = "★", TextColor = Color.FromArgb("#F59E0B"), FontSize = 18, VerticalOptions = LayoutOptions.Center }, 2, 0);

            var card = Ui.Card(row, new Thickness(12));
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await card.ScaleToAsync(0.97, 60);
                    await card.ScaleToAsync(1, 90);
                    await _page.OpenAsync(entry);
                })
            });

            var copy = new SwipeItemView
            {
                Content = new Border
                {
                    BackgroundColor = Ui.Indigo,
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

            Opacity = 0;
            _ = this.FadeToAsync(1, 250);
        }
    }
}

using Microsoft.Maui.Controls.Shapes;

namespace PasswordVault.Mobile;

/// <summary>
/// Farben, Stile und kleine Bausteine â€“ gleiche Farbwelt wie die Windows-App (Violett â†’ Indigo â†’ Cyan).
/// </summary>
public static class Ui
{
    public static readonly Color Bg = Color.FromArgb("#14121F");
    public static readonly Color Surface = Color.FromArgb("#1F1C2E");
    public static readonly Color Surface2 = Color.FromArgb("#2A2640");
    public static readonly Color Stroke = Color.FromArgb("#36314F");
    public static readonly Color Text = Color.FromArgb("#F4F3FF");
    public static readonly Color Muted = Color.FromArgb("#A09CB8");
    public static readonly Color Violet = Color.FromArgb("#8B5CF6");
    public static readonly Color Indigo = Color.FromArgb("#6366F1");
    public static readonly Color Cyan = Color.FromArgb("#06B6D4");
    public static readonly Color Danger = Color.FromArgb("#F43F5E");
    public static readonly Color Success = Color.FromArgb("#22C55E");

    public static Brush AccentGradient => new LinearGradientBrush(
        new GradientStopCollection
        {
            new GradientStop(Violet, 0f),
            new GradientStop(Indigo, 0.55f),
            new GradientStop(Cyan, 1f)
        },
        new Point(0, 0), new Point(1, 1));

    private static readonly Color[] AvatarColors =
    {
        Violet, Indigo, Cyan, Color.FromArgb("#EC4899"), Color.FromArgb("#F59E0B"),
        Color.FromArgb("#10B981"), Color.FromArgb("#3B82F6"), Color.FromArgb("#EF4444")
    };

    public static void ApplyStyles(ResourceDictionary resources)
    {
        resources.Add(new Style(typeof(ContentPage))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = Bg } }
        });
        resources.Add(new Style(typeof(Label))
        {
            Setters = { new Setter { Property = Label.TextColorProperty, Value = Text } }
        });
        resources.Add(new Style(typeof(Entry))
        {
            Setters =
            {
                new Setter { Property = Entry.TextColorProperty, Value = Text },
                new Setter { Property = Entry.PlaceholderColorProperty, Value = Muted },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent },
                new Setter { Property = Entry.FontSizeProperty, Value = 17d }
            }
        });
        resources.Add(new Style(typeof(Editor))
        {
            Setters =
            {
                new Setter { Property = Editor.TextColorProperty, Value = Text },
                new Setter { Property = Editor.PlaceholderColorProperty, Value = Muted },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent }
            }
        });
        resources.Add(new Style(typeof(Switch))
        {
            Setters = { new Setter { Property = Switch.OnColorProperty, Value = Violet } }
        });
        resources.Add(new Style(typeof(Slider))
        {
            Setters =
            {
                new Setter { Property = Slider.MinimumTrackColorProperty, Value = Violet },
                new Setter { Property = Slider.MaximumTrackColorProperty, Value = Stroke }
            }
        });
    }

    /// <summary>Wurzel jeder Seite: ein Grid, damit Hinweise (Toast) darÃ¼ber gelegt werden kÃ¶nnen.</summary>
    public static Grid PageRoot(View content) => new() { Children = { content } };

    public static Border Card(View content, Thickness? padding = null) => new()
    {
        BackgroundColor = Surface,
        Stroke = Stroke,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 18 },
        Padding = padding ?? new Thickness(16, 12),
        Content = content
    };

    /// <summary>Beschriftetes Feld im Karten-Stil; <paramref name="trailing"/> sind KnÃ¶pfe rechts daneben.</summary>
    public static Border Field(string caption, View input, params View[] trailing)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 4
        };
        grid.Add(new Label { Text = caption.ToUpperInvariant(), FontSize = 11, TextColor = Muted, CharacterSpacing = 1 }, 0, 0);
        grid.Add(input, 0, 1);
        for (int i = 0; i < trailing.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.Add(trailing[i], i + 1, 0);
            Grid.SetRowSpan(trailing[i], 2);
        }
        return Card(grid, new Thickness(16, 10, 8, 6));
    }

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        Background = AccentGradient,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        FontSize = 17,
        CornerRadius = 16,
        HeightRequest = 54
    };

    public static Button SecondaryButton(string text, Color? color = null) => new()
    {
        Text = text,
        BackgroundColor = Surface2,
        TextColor = color ?? Text,
        FontSize = 16,
        CornerRadius = 16,
        HeightRequest = 50
    };

    /// <summary>Kleiner runder Knopf mit Symbol (Kopieren, Anzeigen, â€¦).</summary>
    public static Button IconButton(string glyph, string description, EventHandler onClick)
    {
        var b = new Button
        {
            Text = glyph,
            FontSize = 18,
            BackgroundColor = Surface2,
            TextColor = Text,
            CornerRadius = 20,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetDescription(b, description);
        b.Clicked += onClick;
        return b;
    }

    public static Border Avatar(string title, double size = 44)
    {
        var letter = string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[..1].ToUpperInvariant();
        var color = AvatarColors[(int)((uint)StableHash(title) % AvatarColors.Length)];
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = size * 0.32 },
            Background = new LinearGradientBrush(
                new GradientStopCollection { new GradientStop(color, 0f), new GradientStop(color.WithLuminosity(0.35f), 1f) },
                new Point(0, 0), new Point(1, 1)),
            Content = new Label
            {
                Text = letter,
                FontSize = size * 0.42,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    // string.GetHashCode ist pro Prozess zufÃ¤llig â€“ hier soll die Farbe stabil bleiben
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    /// <summary>StÃ¤rke-Balken; die zurÃ¼ckgegebene Aktion aktualisiert ihn fÃ¼r ein Passwort.</summary>
    public static (View View, Action<string> Update) StrengthMeter()
    {
        var bar = new ProgressBar { ProgressColor = Danger, HeightRequest = 6 };
        var label = new Label { FontSize = 12, TextColor = Muted };
        void Update(string pw)
        {
            var r = PasswordVault.Core.Services.PasswordStrength.Evaluate(pw);
            bar.ProgressColor = r.Score switch { < 35 => Danger, < 60 => Color.FromArgb("#F59E0B"), < 80 => Cyan, _ => Success };
            _ = bar.ProgressTo(r.Score / 100.0, 300, Easing.CubicOut);
            label.Text = r.Score == 0 ? "" : $"{r.Label} Â· {r.EntropyBits:0} Bit";
        }
        return (new VerticalStackLayout { Spacing = 4, Padding = new Thickness(4, 0), Children = { bar, label } }, Update);
    }

    /// <summary>LÃ¤sst Elemente nacheinander von unten einblenden.</summary>
    public static async void AnimateIn(params VisualElement[] views)
    {
        foreach (var v in views)
        {
            v.Opacity = 0;
            v.TranslationY = 24;
        }
        for (int i = 0; i < views.Length; i++)
        {
            var v = views[i];
            _ = v.FadeToAsync(1, 380, Easing.CubicOut);
            _ = v.TranslateToAsync(0, 0, 420, Easing.CubicOut);
            await Task.Delay(55);
        }
    }

    public static async Task Shake(VisualElement view)
    {
        foreach (var x in new[] { -14, 12, -9, 6, -3, 0 })
            await view.TranslateToAsync(x, 0, 45);
    }

    /// <summary>Kurzer Hinweis am unteren Rand, der von selbst verschwindet.</summary>
    public static async void Toast(Page page, string text, bool error = false)
    {
        if (page is NavigationPage nav) page = nav.CurrentPage;
        if (page is not ContentPage { Content: Grid root }) return;

        var toast = new Border
        {
            BackgroundColor = error ? Danger : Surface2,
            Stroke = Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(20, 12),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(24, 0, 24, 36),
            InputTransparent = true,
            Opacity = 0,
            TranslationY = 30,
            Content = new Label { Text = text, FontSize = 15, TextColor = Colors.White }
        };
        root.Add(toast);
        Grid.SetRowSpan(toast, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(toast, Math.Max(1, root.ColumnDefinitions.Count));
        SemanticScreenReader.Announce(text);

        await Task.WhenAll(toast.FadeToAsync(1, 200), toast.TranslateToAsync(0, 0, 260, Easing.CubicOut));
        await Task.Delay(1800);
        await Task.WhenAll(toast.FadeToAsync(0, 220), toast.TranslateToAsync(0, 20, 220));
        root.Remove(toast);
    }
}

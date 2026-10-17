using Microsoft.Maui.Controls.Shapes;
using UIKit;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace PasswordVault.Mobile;

/// <summary>
/// Design-Tokens und Bausteine – Gegenstück zu BrowserExtension/src/ui/tokens.css:
/// monochrom und dunkel, Akzent Weiß, Rot/Grün nur für Fehler, Löschen und Erfolg.
/// Animiert werden nur Transform (Translation/Scale) und Opacity.
/// </summary>
public static class Ui
{
    // ===== Farben =====
    public static readonly Color Bg = Color.FromArgb("#050505");
    public static readonly Color Surface = Color.FromArgb("#0DFFFFFF");
    public static readonly Color SurfaceStrong = Color.FromArgb("#17FFFFFF");
    public static readonly Color Stroke = Color.FromArgb("#14FFFFFF");
    public static readonly Color StrokeStrong = Color.FromArgb("#29FFFFFF");
    public static readonly Color Text = Color.FromArgb("#F5F5F5");
    public static readonly Color Muted = Color.FromArgb("#A3A3A3");
    public static readonly Color Subtle = Color.FromArgb("#6B6B6B");
    public static readonly Color Accent = Colors.White;
    public static readonly Color OnAccent = Color.FromArgb("#0A0A0A");
    public static readonly Color Danger = Color.FromArgb("#F87171");
    public static readonly Color Success = Color.FromArgb("#4ADE80");
    // Glas-Fläche für Toasts (deckend genug, da MAUI keinen Hintergrund-Blur kennt)
    public static readonly Color Glass = Color.FromArgb("#EB161616");

    // ===== Schrift (Inter, lokal eingebunden) =====
    public const string Font = "Inter";
    public const string FontSemiBold = "InterSemiBold";
    public const string FontBold = "InterBold";
    public const string FontMono = "Menlo";

    // ===== Radien & Abstände =====
    public const double RadiusSm = 8;
    public const double RadiusMd = 10;
    public const double RadiusLg = 14;
    public const double RadiusXl = 18;
    public const double Space = 16;

    // ===== Bewegung: cubic-bezier(0.22, 1, 0.36, 1), 150–300 ms =====
    public const uint Fast = 150;
    public const uint Normal = 220;
    public const uint Slow = 300;
    public const int StaggerStep = 45;
    private const int MaxStaggered = 10;
    public static readonly Easing Ease = CubicBezier(0.22, 1, 0.36, 1);

    private const string ReduceMotionKey = "reduceMotion";

    /// <summary>Eigene Einstellung oder iOS "Bewegung reduzieren" schalten Animationen fast ganz ab.</summary>
    public static bool ReduceMotion
    {
        get => Preferences.Default.Get(ReduceMotionKey, false) || UIAccessibility.IsReduceMotionEnabled;
        set => Preferences.Default.Set(ReduceMotionKey, value);
    }

    public static bool ReduceMotionSetting => Preferences.Default.Get(ReduceMotionKey, false);

    private static uint D(uint ms) => ReduceMotion ? 1 : ms;

    public static void ApplyStyles(ResourceDictionary resources)
    {
        resources.Add(new Style(typeof(ContentPage))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = Bg } }
        });
        resources.Add(new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.TextColorProperty, Value = Text },
                new Setter { Property = Label.FontFamilyProperty, Value = Font }
            }
        });
        resources.Add(new Style(typeof(Entry))
        {
            Setters =
            {
                new Setter { Property = Entry.TextColorProperty, Value = Text },
                new Setter { Property = Entry.PlaceholderColorProperty, Value = Subtle },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent },
                new Setter { Property = Entry.FontSizeProperty, Value = 17d },
                new Setter { Property = Entry.FontFamilyProperty, Value = Font }
            }
        });
        resources.Add(new Style(typeof(Editor))
        {
            Setters =
            {
                new Setter { Property = Editor.TextColorProperty, Value = Text },
                new Setter { Property = Editor.PlaceholderColorProperty, Value = Subtle },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent },
                new Setter { Property = Editor.FontFamilyProperty, Value = Font }
            }
        });
        resources.Add(new Style(typeof(Button))
        {
            Setters = { new Setter { Property = Button.FontFamilyProperty, Value = FontSemiBold } }
        });
        resources.Add(new Style(typeof(Switch))
        {
            Setters =
            {
                new Setter { Property = Switch.OnColorProperty, Value = Accent },
                new Setter { Property = Switch.ThumbColorProperty, Value = Muted }
            }
        });
        resources.Add(new Style(typeof(Slider))
        {
            Setters =
            {
                new Setter { Property = Slider.MinimumTrackColorProperty, Value = Accent },
                new Setter { Property = Slider.MaximumTrackColorProperty, Value = StrokeStrong },
                new Setter { Property = Slider.ThumbColorProperty, Value = Accent }
            }
        });
    }

    /// <summary>Wurzel jeder Seite: ein Grid, damit Hinweise (Toast) darüber gelegt werden können.</summary>
    public static Grid PageRoot(View content) => new() { Children = { content } };

    /// <summary>Halbtransparente Fläche mit 1px-Rand rgba(255,255,255,0.08).</summary>
    public static Border Card(View content, Thickness? padding = null) => new()
    {
        BackgroundColor = Surface,
        Stroke = Stroke,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = RadiusLg },
        Padding = padding ?? new Thickness(16, 12),
        Content = content
    };

    public static Label Caption(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        TextColor = Subtle,
        CharacterSpacing = 1.2
    };

    /// <summary>Beschriftetes Feld im Karten-Stil; <paramref name="trailing"/> sind Knöpfe rechts daneben.</summary>
    public static Border Field(string caption, View input, params View[] trailing)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 6
        };
        grid.Add(Caption(caption), 0, 0);
        grid.Add(input, 0, 1);
        for (int i = 0; i < trailing.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.Add(trailing[i], i + 1, 0);
            Grid.SetRowSpan(trailing[i], 2);
        }
        return Card(grid, new Thickness(16, 10, 8, 6));
    }

    public static Button PrimaryButton(string text) => Pressable(new Button
    {
        Text = text,
        BackgroundColor = Accent,
        TextColor = OnAccent,
        FontFamily = FontSemiBold,
        FontSize = 17,
        CornerRadius = (int)RadiusMd,
        HeightRequest = 52
    });

    public static Button SecondaryButton(string text, Color? color = null) => Pressable(new Button
    {
        Text = text,
        BackgroundColor = Surface,
        BorderColor = Stroke,
        BorderWidth = 1,
        TextColor = color ?? Text,
        FontSize = 16,
        CornerRadius = (int)RadiusMd,
        HeightRequest = 50
    });

    /// <summary>Klick-Rückmeldung: Scale 0.97 beim Drücken.</summary>
    public static T Pressable<T>(T button) where T : Button
    {
        button.Pressed += (_, _) => _ = button.ScaleToAsync(0.97, D(Fast), Ease);
        button.Released += (_, _) => _ = button.ScaleToAsync(1, D(Normal), Ease);
        return button;
    }

    // ===== Symbole als Vektorpfade (24er-Raster, Strichstärke 2 – wie die Inline-SVGs der Erweiterung) =====
    public static class Icons
    {
        public const string Lock = "M7 11V8a5 5 0 0 1 10 0v3 M6 11h12a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z";
        public const string Eye = "M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z M12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6z";
        public const string EyeOff = "M3 3l18 18 M10.6 5.1A10 10 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.9 M6.6 6.6C3.9 8.3 2 12 2 12s3.5 7 10 7c1.9 0 3.5-.6 4.9-1.4 M9.9 9.9a3 3 0 0 0 4.2 4.2";
        public const string Copy = "M10 8h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2z M16 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h3";
        public const string External = "M14 4h6v6 M20 4l-9 9 M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5";
        public const string Refresh = "M21 12a9 9 0 1 1-3-6.7L21 8 M21 3v5h-5";
        public const string Key = "M15 7a4 4 0 1 1-4 4 M11 11l-8 8 M6 16l2 2 M9 13l2 2";
        public const string Star = "M12 3l2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z";
        public const string Plus = "M12 5v14 M5 12h14";
        public const string Check = "M5 12.5l4.5 4.5L19 7.5";
        public const string Alert = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7.5v5.5 M12 16.5v.01";
    }

    public static View Icon(string data, double size = 20, Color? color = null, bool filled = false)
    {
        var path = new Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            Stroke = color ?? Text,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = filled ? color ?? Text : null,
            Aspect = Stretch.None,
            WidthRequest = 24,
            HeightRequest = 24,
            Scale = size / 24,
            InputTransparent = true
        };
        return new Grid
        {
            WidthRequest = size,
            HeightRequest = size,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            Children = { path }
        };
    }

    /// <summary>Runder Symbol-Knopf (Kopieren, Anzeigen, …) mit Drück-Rückmeldung.</summary>
    public static Border IconButton(string iconData, string description, EventHandler onClick)
    {
        var b = new Border
        {
            BackgroundColor = Surface,
            Stroke = Stroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            WidthRequest = 44,
            HeightRequest = 44,
            VerticalOptions = LayoutOptions.Center,
            Content = Icon(iconData, 18)
        };
        SemanticProperties.SetDescription(b, description);
        AutomationProperties.SetIsInAccessibleTree(b, true);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            onClick(b, e);
            await b.ScaleToAsync(0.97, D(Fast), Ease);
            await b.ScaleToAsync(1, D(Normal), Ease);
        };
        b.GestureRecognizers.Add(tap);
        return b;
    }

    public static void SetIcon(Border iconButton, string iconData) => iconButton.Content = Icon(iconData, 18);

    public static Border Avatar(string title, double size = 44)
    {
        var letter = string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[..1].ToUpperInvariant();
        // Monochrom: stabiler Grauton pro Titel, damit Einträge unterscheidbar bleiben
        var light = 0.10f + Math.Abs(StableHash(title) % 6) * 0.03f;
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            Stroke = Stroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = size * 0.28 },
            BackgroundColor = Color.FromHsla(0, 0, light),
            Content = new Label
            {
                Text = letter,
                FontSize = size * 0.4,
                FontFamily = FontSemiBold,
                TextColor = Text,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    // string.GetHashCode ist pro Prozess zufällig – hier soll der Grauton stabil bleiben
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (var c in s.ToLowerInvariant()) h = h * 31 + c;
            return h;
        }
    }

    /// <summary>Stärke-Balken (animiert über ScaleX); die Aktion aktualisiert ihn für ein Passwort.</summary>
    public static (View View, Action<string> Update) StrengthMeter()
    {
        var fill = new BoxView { Color = Accent, CornerRadius = 2, AnchorX = 0, ScaleX = 0 };
        var track = new Grid
        {
            HeightRequest = 4,
            Children = { new BoxView { Color = SurfaceStrong, CornerRadius = 2 }, fill }
        };
        var label = new Label { FontSize = 12, TextColor = Muted };
        void Update(string pw)
        {
            var r = PasswordVault.Core.Services.PasswordStrength.Evaluate(pw);
            // Nur "sehr schwach" gilt als Fehler; sonst bleibt der Balken weiß
            fill.Color = r.Level == PasswordVault.Core.Services.StrengthLevel.VeryWeak ? Danger : Accent;
            fill.CancelAnimations();
            _ = fill.ScaleXToAsync(Math.Max(r.Score, 2) / 100.0, D(Slow), Ease);
            label.Text = r.Score == 0 ? "" : $"{r.Label} · {r.EntropyBits:0} Bit";
        }
        return (new VerticalStackLayout { Spacing = 6, Padding = new Thickness(4, 0), Children = { track, label } }, Update);
    }

    /// <summary>Elemente gestaffelt einblenden (45 ms Abstand, ab dem 10. gemeinsam).</summary>
    public static void AnimateIn(params VisualElement[] views)
    {
        if (ReduceMotion) return;
        for (int i = 0; i < views.Length; i++)
            _ = FadeUp(views[i], (uint)(Math.Min(i, MaxStaggered) * StaggerStep));
    }

    public static async Task FadeUp(VisualElement v, uint delayMs = 0)
    {
        v.Opacity = 0;
        v.TranslationY = 8;
        if (delayMs > 0) await Task.Delay((int)delayMs);
        await Task.WhenAll(v.FadeToAsync(1, Slow, Ease), v.TranslateToAsync(0, 0, Slow, Ease));
    }

    /// <summary>Modal-Einblendung: Fade + Scale 0.95 → 1 + leicht nach oben.</summary>
    public static async Task PopIn(VisualElement v)
    {
        if (ReduceMotion) return;
        v.Opacity = 0;
        v.Scale = 0.95;
        v.TranslationY = 8;
        await Task.WhenAll(v.FadeToAsync(1, Normal, Ease), v.ScaleToAsync(1, Normal, Ease), v.TranslateToAsync(0, 0, Normal, Ease));
    }

    /// <summary>Seitenwechsel als Crossfade statt Wischbewegung.</summary>
    public static async Task Push(INavigation nav, Page page)
    {
        if (ReduceMotion || page is not ContentPage { Content: { } content })
        {
            await nav.PushAsync(page, false);
            return;
        }
        content.Opacity = 0;
        await nav.PushAsync(page, false);
        await content.FadeToAsync(1, Normal, Ease);
    }

    public static async Task Shake(VisualElement view)
    {
        if (ReduceMotion) return;
        foreach (var x in new[] { -8, 7, -5, 3, 0 })
            await view.TranslateToAsync(x, 0, 60, Ease);
    }

    /// <summary>Hinweis, der seitlich hereingleitet; die Leiste unten läuft bis zum Ausblenden ab.</summary>
    public static async void Toast(Page page, string text, bool error = false)
    {
        if (page is NavigationPage nav) page = nav.CurrentPage;
        if (page is not ContentPage { Content: Grid root }) return;

        const int shownMs = 2400;
        var bar = new BoxView { Color = Muted, HeightRequest = 2, VerticalOptions = LayoutOptions.End, AnchorX = 0 };
        var toast = new Border
        {
            BackgroundColor = Glass,
            Stroke = StrokeStrong,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = RadiusLg },
            Padding = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 16, 32),
            InputTransparent = true,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Radius = 30, Opacity = 0.5f, Offset = new Point(0, 12) },
            Content = new Grid
            {
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Padding = new Thickness(16, 12, 18, 14),
                        Children =
                        {
                            Icon(error ? Icons.Alert : Icons.Check, 16, error ? Danger : Success),
                            new Label { Text = text, FontSize = 15, FontFamily = FontSemiBold, VerticalOptions = LayoutOptions.Center }
                        }
                    },
                    bar
                }
            }
        };
        root.Add(toast);
        Grid.SetRowSpan(toast, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(toast, Math.Max(1, root.ColumnDefinitions.Count));
        SemanticScreenReader.Announce(text);

        toast.Opacity = 0;
        toast.TranslationX = 120;
        await Task.WhenAll(toast.FadeToAsync(1, D(Normal), Ease), toast.TranslateToAsync(0, 0, D(Slow), Ease));
        await bar.ScaleXToAsync(0, ReduceMotion ? 1u : shownMs, Easing.Linear);
        if (ReduceMotion) await Task.Delay(shownMs);
        await Task.WhenAll(toast.FadeToAsync(0, D(Normal), Ease), toast.TranslateToAsync(120, 0, D(Normal), Ease));
        root.Remove(toast);
    }

    /// <summary>CSS-Easing cubic-bezier(x1, y1, x2, y2) als MAUI-Easing.</summary>
    private static Easing CubicBezier(double x1, double y1, double x2, double y2)
    {
        double Bez(double t, double a, double b) => 3 * a * t * (1 - t) * (1 - t) + 3 * b * t * t * (1 - t) + t * t * t;
        return new Easing(x =>
        {
            // t zu x per Bisektion suchen (x ist monoton in t), dann y berechnen
            double lo = 0, hi = 1, t = x;
            for (int i = 0; i < 20; i++)
            {
                t = (lo + hi) / 2;
                if (Bez(t, x1, x2) < x) lo = t; else hi = t;
            }
            return Bez(t, y1, y2);
        });
    }
}

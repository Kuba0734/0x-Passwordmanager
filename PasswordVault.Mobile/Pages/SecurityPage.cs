using Microsoft.Maui.Controls.Shapes;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Sicherheitscheck wie am PC: Bewertung, schwache/doppelte/alte/HTTP-Einträge, Tippen öffnet den Eintrag.
/// </summary>
public class SecurityPage : ContentPage
{
    private readonly ScoreRing _ring = new();
    private readonly GraphicsView _ringView;
    private readonly Label _score = new() { FontSize = 40, FontFamily = Ui.FontBold, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Label _title = new() { FontSize = 20, FontFamily = Ui.FontBold, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _description = new() { FontSize = 14, TextColor = Ui.Muted, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _meta = new() { FontSize = 12, TextColor = Ui.Subtle, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _issuesTitle = Ui.Section("Alle Hinweise");
    private readonly Button _showAll = new() { Text = "Alle zeigen", BackgroundColor = Colors.Transparent, TextColor = Ui.Muted, FontSize = 14, Padding = 0, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End };
    private readonly VerticalStackLayout _issues = new() { Spacing = 8 };
    private readonly Dictionary<string, Label> _counts = new();
    private SecurityReport? _report;
    private string? _kind;

    public SecurityPage()
    {
        Title = "Sicherheit";

        _ringView = new GraphicsView { Drawable = _ring, WidthRequest = 150, HeightRequest = 150, HorizontalOptions = LayoutOptions.Center };
        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(0, 8, 0, 4),
            Children = { new Grid { HorizontalOptions = LayoutOptions.Center, Children = { _ringView, _score } }, _title, _description, _meta }
        };

        var stats = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 12,
            RowSpacing = 12
        };
        stats.Add(StatCard("weak", "Schwach"), 0, 0);
        stats.Add(StatCard("reused", "Doppelt"), 1, 0);
        stats.Add(StatCard("old", "Älter als 1 Jahr"), 0, 1);
        stats.Add(StatCard("insecure", "Ohne HTTPS"), 1, 1);

        _showAll.Clicked += (_, _) => ShowIssues(null);
        var issuesHeader = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        issuesHeader.Add(_issuesTitle, 0, 0);
        issuesHeader.Add(_showAll, 1, 0);

        var views = new View[] { header, stats, issuesHeader, _issues };
        var stack = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(16, 8, 16, 40) };
        foreach (var v in views) stack.Add(v);
        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(views);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!App.Store.IsUnlocked) return;
        Analyze();
    }

    private void Analyze()
    {
        var r = _report = SecurityAnalyzer.Analyze(App.Store.GetEntries());

        Ui.CountUp(_counts["weak"], r.Weak.Count);
        Ui.CountUp(_counts["reused"], r.Reused.Count);
        Ui.CountUp(_counts["old"], r.Old.Count);
        Ui.CountUp(_counts["insecure"], r.Insecure.Count);

        // Monochrom: Grün nur für "ausgezeichnet", Rot nur für "kritisch"
        (_title.Text, _description.Text, _ring.Color) = r.Score switch
        {
            _ when r.Total == 0 => ("Noch nichts zu prüfen", "Sobald du Einträge anlegst, bewertet der Sicherheitscheck deine Passwörter.", Ui.Accent),
            >= 90 => ("Ausgezeichnet", "Deine Passwörter sind stark und einzigartig. So bleibt es sicher.", Ui.Success),
            >= 70 => ("Gut – mit Luft nach oben", "Ein paar Einträge solltest du verbessern. Tippe unten auf einen Hinweis.", Ui.Accent),
            >= 40 => ("Verbesserungswürdig", "Mehrere schwache oder doppelte Passwörter gefunden. Ersetze sie mit dem Generator.", Ui.Muted),
            _ => ("Kritisch", "Viele Passwörter sind leicht zu knacken oder mehrfach verwendet. Jetzt handeln!", Ui.Danger)
        };
        _meta.Text = $"{r.Total} Einträge geprüft · Stand {DateTime.Now:HH:mm} Uhr";

        Ui.CountUp(_score, r.Score, v =>
        {
            _ring.Value = v / 100;
            _ringView.Invalidate();
        });
        ShowIssues(_kind);
    }

    private Border StatCard(string kind, string label)
    {
        var count = new Label { Text = "0", FontSize = 28, FontFamily = Ui.FontBold };
        _counts[kind] = count;
        var card = Ui.Card(new VerticalStackLayout
        {
            Spacing = 2,
            Children = { count, new Label { Text = label, FontSize = 13, TextColor = Ui.Muted } }
        }, new Thickness(16, 12));
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                ShowIssues(kind);
                await card.ScaleToAsync(0.97, Ui.Fast, Ui.Ease);
                await card.ScaleToAsync(1, Ui.Normal, Ui.Ease);
            })
        });
        SemanticProperties.SetDescription(card, $"{label} anzeigen");
        return card;
    }

    private void ShowIssues(string? kind)
    {
        if (_report == null) return;
        _kind = kind;
        var r = _report;
        var list = new List<(PasswordEntry Entry, string Reason, string Hint)>();

        if (kind is null or "weak")
            list.AddRange(r.Weak.Select(e => (e, "Schwach", "Leicht zu erraten – ersetze es durch ein generiertes Passwort.")));
        if (kind is null or "reused")
            foreach (var group in r.Reused.GroupBy(e => e.Password))
            {
                var names = group.Select(e => e.Title).ToList();
                list.AddRange(group.Select(e => (e, "Doppelt", "Gleiches Passwort wie: " + string.Join(", ", names.Where(n => n != e.Title).Take(3)))));
            }
        if (kind is null or "old")
            list.AddRange(r.Old.Select(e => (e, "Alt", "Seit über einem Jahr nicht geändert.")));
        if (kind is null or "insecure")
            list.AddRange(r.Insecure.Select(e => (e, "HTTP", "Die Website nutzt kein HTTPS – Anmeldedaten könnten mitgelesen werden.")));

        _issuesTitle.Text = kind switch
        {
            "weak" => "Schwache Passwörter",
            "reused" => "Doppelte Passwörter",
            "old" => "Alte Passwörter",
            "insecure" => "Ohne HTTPS",
            _ => "Alle Hinweise"
        };
        _showAll.IsVisible = kind != null;

        _issues.Clear();
        if (list.Count == 0)
        {
            var allGood = new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(20, 24),
                Children =
                {
                    Ui.Icon(Ui.Icons.Check, 36, Ui.Success),
                    new Label
                    {
                        Text = kind == null ? "Keine Probleme gefunden – weiter so!" : "In dieser Kategorie ist alles in Ordnung.",
                        TextColor = Ui.Muted,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
            _issues.Add(allGood);
            _ = Ui.PopIn(allGood);
            return;
        }

        var rows = list.Select(i => IssueRow(i.Entry, i.Reason, i.Hint)).ToArray();
        foreach (var row in rows) _issues.Add(row);
        Ui.AnimateIn(rows);
    }

    private Border IssueRow(PasswordEntry entry, string reason, string hint)
    {
        var badge = new Border
        {
            // Nur "schwach" ist ein Fehler und bekommt den Gefahrton
            BackgroundColor = reason == "Schwach" ? Ui.Danger.WithAlpha(0.18f) : Ui.SurfaceStrong,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(8, 3),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = reason, FontSize = 12, FontFamily = Ui.FontSemiBold, TextColor = reason == "Schwach" ? Ui.Danger : Ui.Text }
        };
        var text = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = entry.Title, FontSize = 16, FontFamily = Ui.FontBold, LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = hint, FontSize = 13, TextColor = Ui.Muted }
            }
        };
        var row = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        row.Add(Ui.Avatar(entry.Title, 40), 0, 0);
        row.Add(text, 1, 0);
        row.Add(badge, 2, 0);

        var card = Ui.Card(row, new Thickness(12));
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                await card.ScaleToAsync(0.97, Ui.Fast, Ui.Ease);
                await card.ScaleToAsync(1, Ui.Normal, Ui.Ease);
                await Ui.Push(Navigation, new EntryPage(entry.Id));
            })
        });
        SemanticProperties.SetDescription(card, $"{entry.Title}: {reason}");
        return card;
    }

    /// <summary>Ring um die Punktzahl; Wert 0–1.</summary>
    private sealed class ScoreRing : IDrawable
    {
        public double Value { get; set; }
        public Color Color { get; set; } = Ui.Accent;

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float stroke = 10;
            var r = new RectF(rect.X + stroke / 2, rect.Y + stroke / 2, rect.Width - stroke, rect.Height - stroke);
            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeColor = Ui.SurfaceStrong;
            canvas.DrawEllipse(r);
            if (Value <= 0) return;
            canvas.StrokeColor = Color;
            // Oben beginnend im Uhrzeigersinn
            var end = 90 - (float)(Math.Min(Value, 0.9999) * 360);
            canvas.DrawArc(r, 90, end, true, false);
        }
    }
}

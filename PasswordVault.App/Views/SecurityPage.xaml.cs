using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;

namespace PasswordVault.App.Views;

public sealed class SecurityIssue
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Hint { get; init; }
    public required string Reason { get; init; }
    public required Brush Tint { get; init; }

    public override string ToString() => $"{Title}: {Reason}";
}

public sealed partial class SecurityPage : Page
{
    private readonly ObservableCollection<SecurityIssue> _issues = new();
    private SecurityReport? _report;
    private DispatcherQueueTimer? _countTimer;

    // Monochrom: nur "schwach" ist ein Fehler und bekommt den Gefahrton, der Rest bleibt neutral
    private static readonly Brush DangerTint = (Brush)Application.Current.Resources["DangerSurfaceBrush"];
    private static readonly Brush WarningTint = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
    private static readonly Brush InfoTint = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PurpleTint = InfoTint;

    public SecurityPage()
    {
        InitializeComponent();
        IssueList.ItemsSource = _issues;
        Loaded += (_, _) =>
        {
            App.Store.Changed += Store_Changed;
            Motion.Stagger(Root);
            Motion.Stagger(StatsGrid, Motion.StaggerStep, Motion.StaggerDelay(2));
            Analyze(animate: true);
        };
        Unloaded += (_, _) =>
        {
            App.Store.Changed -= Store_Changed;
            _countTimer?.Stop();
        };
    }

    private void Store_Changed(object? sender, VaultChangedEventArgs e)
        => DispatcherQueue.TryEnqueue(() => Analyze(animate: false));

    private void Analyze(bool animate)
    {
        if (!App.Store.IsUnlocked) return;
        var entries = App.Store.GetEntries();
        _report = SecurityAnalyzer.Analyze(entries);
        var r = _report;

        SetCount(WeakCount, r.Weak.Count, animate);
        SetCount(ReusedCount, r.Reused.Count, animate);
        SetCount(OldCount, r.Old.Count, animate);
        SetCount(InsecureCount, r.Insecure.Count, animate);

        var (title, description, color) = r.Score switch
        {
            _ when r.Total == 0 => ("Noch nichts zu prüfen",
                "Sobald du Einträge anlegst, bewertet der Sicherheitscheck deine Passwörter.", Color.FromArgb(255, 255, 255, 255)),
            >= 90 => ("Ausgezeichnet",
                "Deine Passwörter sind stark und einzigartig. So bleibt es sicher.", Color.FromArgb(255, 74, 222, 128)),
            >= 70 => ("Gut – mit Luft nach oben",
                "Ein paar Einträge solltest du verbessern. Tippe unten auf einen Hinweis.", Color.FromArgb(255, 255, 255, 255)),
            >= 40 => ("Verbesserungswürdig",
                "Mehrere schwache oder doppelte Passwörter gefunden. Ersetze sie mit dem Generator.", Color.FromArgb(255, 163, 163, 163)),
            _ => ("Kritisch",
                "Viele Passwörter sind leicht zu knacken oder mehrfach verwendet. Jetzt handeln!", Color.FromArgb(255, 248, 113, 113))
        };
        ScoreTitle.Text = title;
        ScoreDescription.Text = description;
        ScoreRing.Foreground = new SolidColorBrush(color);
        ScoreMeta.Text = $"{r.Total} Einträge geprüft · Stand {DateTime.Now:HH:mm} Uhr";

        AnimateScore(r.Score, animate);
        ShowIssues(null);
    }

    private static void SetCount(TextBlock target, int value, bool animate)
    {
        if (animate) Motion.CountUp(target, value);
        else target.Text = value.ToString();
    }

    private void AnimateScore(int target, bool animate)
    {
        _countTimer?.Stop();
        if (!animate || !Motion.Enabled)
        {
            ScoreRing.Value = target;
            ScoreText.Text = target.ToString();
            return;
        }

        var start = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(1100);
        _countTimer = DispatcherQueue.CreateTimer();
        _countTimer.Interval = TimeSpan.FromMilliseconds(16);
        _countTimer.Tick += (t, _) =>
        {
            var p = Math.Min(1, (DateTime.UtcNow - start) / duration);
            var eased = 1 - Math.Pow(1 - p, 3); // ease-out cubic
            var value = target * eased;
            ScoreRing.Value = value;
            ScoreText.Text = ((int)Math.Round(value)).ToString();
            if (p >= 1) t.Stop();
        };
        _countTimer.Start();
    }

    private void ShowIssues(string? kind)
    {
        if (_report == null) return;
        var r = _report;
        var list = new List<SecurityIssue>();

        void AddAll(IEnumerable<PasswordEntry> entries, string reason, string hint, Brush tint)
        {
            foreach (var e in entries)
                list.Add(new SecurityIssue { Id = e.Id, Title = e.Title, Hint = hint, Reason = reason, Tint = tint });
        }

        if (kind is null or "weak")
            AddAll(r.Weak, "Schwach", "Leicht zu erraten – ersetze es durch ein generiertes Passwort.", DangerTint);
        if (kind is null or "reused")
        {
            foreach (var group in r.Reused.GroupBy(e => e.Password))
            {
                var names = group.Select(e => e.Title).ToList();
                foreach (var e in group)
                    list.Add(new SecurityIssue
                    {
                        Id = e.Id, Title = e.Title, Reason = "Doppelt", Tint = WarningTint,
                        Hint = "Gleiches Passwort wie: " + string.Join(", ", names.Where(n => n != e.Title).Take(3))
                    });
            }
        }
        if (kind is null or "old")
            AddAll(r.Old, "Alt", "Seit über einem Jahr nicht geändert.", InfoTint);
        if (kind is null or "insecure")
            AddAll(r.Insecure, "HTTP", "Die Website nutzt kein HTTPS – Anmeldedaten könnten mitgelesen werden.", PurpleTint);

        IssuesTitle.Text = kind switch
        {
            "weak" => "Schwache Passwörter",
            "reused" => "Mehrfach verwendete Passwörter",
            "old" => "Alte Passwörter",
            "insecure" => "Unverschlüsselte Seiten",
            _ => "Alle Hinweise"
        };
        ShowAllButton.Visibility = kind == null ? Visibility.Collapsed : Visibility.Visible;

        _issues.Clear();
        foreach (var issue in list) _issues.Add(issue);

        bool none = _issues.Count == 0;
        IssueList.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        bool wasHidden = AllGood.Visibility != Visibility.Visible;
        AllGood.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        AllGoodText.Text = kind == null ? "Keine Probleme gefunden – weiter so!" : "In dieser Kategorie ist alles in Ordnung.";
        if (none && wasHidden) Motion.PopIn(AllGoodIcon, 100);
    }

    private void StatCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string kind } button)
        {
            Motion.Pulse(button, 1.03f);
            ShowIssues(kind);
        }
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e) => ShowIssues(null);

    private void IssueList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SecurityIssue issue)
            FindMainPage()?.ShowEntry(issue.Id);
    }

    private MainPage? FindMainPage()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is MainPage page) return page;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

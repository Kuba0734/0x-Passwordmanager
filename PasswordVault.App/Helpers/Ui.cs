using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Collections.Generic;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Kleine Hilfsfunktionen für x:Bind-Funktionsbindungen und gemeinsame UI-Logik.
/// </summary>
public static class Ui
{
    private static readonly (Color A, Color B)[] AvatarPalette =
    {
        (Color.FromArgb(255, 139, 92, 246), Color.FromArgb(255, 99, 102, 241)),
        (Color.FromArgb(255, 6, 182, 212), Color.FromArgb(255, 59, 130, 246)),
        (Color.FromArgb(255, 236, 72, 153), Color.FromArgb(255, 244, 63, 94)),
        (Color.FromArgb(255, 245, 158, 11), Color.FromArgb(255, 239, 68, 68)),
        (Color.FromArgb(255, 34, 197, 94), Color.FromArgb(255, 16, 185, 129)),
        (Color.FromArgb(255, 20, 184, 166), Color.FromArgb(255, 6, 182, 212)),
        (Color.FromArgb(255, 168, 85, 247), Color.FromArgb(255, 236, 72, 153)),
        (Color.FromArgb(255, 59, 130, 246), Color.FromArgb(255, 99, 102, 241)),
    };

    private static readonly Dictionary<int, Brush> AvatarCache = new();

    public static string Initial(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "?";
        foreach (var ch in title.Trim())
            if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString();
        return title.Trim()[0].ToString();
    }

    public static Brush AvatarBrush(string? title)
    {
        var key = StableHash(title ?? "") % AvatarPalette.Length;
        if (AvatarCache.TryGetValue(key, out var cached)) return cached;

        var (a, b) = AvatarPalette[key];
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = a, Offset = 0 },
                new GradientStop { Color = b, Offset = 1 }
            }
        };
        AvatarCache[key] = brush;
        return brush;
    }

    // string.GetHashCode ist pro Prozess randomisiert – Farben sollen stabil bleiben
    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (var ch in text.ToLowerInvariant()) hash = hash * 31 + ch;
            return Math.Abs(hash % 100_000);
        }
    }

    public static string Subtitle(string? username, string? url)
    {
        if (!string.IsNullOrWhiteSpace(username)) return username!;
        return UrlMatcher.GetHost(url) ?? "—";
    }

    public static string Host(string? url) => UrlMatcher.GetHost(url) ?? "";

    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisibleIf(string? value) => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    public static string CategoryGlyph(string? category) => category switch
    {
        Categories.Logins => "",
        Categories.Cards => "",
        Categories.Notes => "",
        _ => ""
    };

    public static string FilterGlyph(string tag) => tag switch
    {
        "favorites" => "",
        "logins" => "",
        "cards" => "",
        "notes" => "",
        "general" => "",
        _ => ""
    };

    public static string RelativeTime(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "gerade eben";
        if (span.TotalHours < 1) return $"vor {(int)span.TotalMinutes} Min.";
        if (span.TotalDays < 1) return $"vor {(int)span.TotalHours} Std.";
        if (span.TotalDays < 30) return $"vor {(int)span.TotalDays} Tagen";
        return utc.ToLocalTime().ToString("dd.MM.yyyy");
    }

    public static Brush StrengthBrush(StrengthLevel level) => level switch
    {
        StrengthLevel.VeryWeak => (Brush)Application.Current.Resources["DangerBrush"],
        StrengthLevel.Weak => new SolidColorBrush(Color.FromArgb(255, 249, 115, 22)),
        StrengthLevel.Medium => (Brush)Application.Current.Resources["WarningBrush"],
        StrengthLevel.Strong => (Brush)Application.Current.Resources["SuccessBrush"],
        StrengthLevel.VeryStrong => (Brush)Application.Current.Resources["VaultGradientBrush"],
        _ => new SolidColorBrush(Colors.Transparent)
    };

    /// <summary>
    /// Füllt einen TextBlock mit farbig hervorgehobenen Ziffern und Sonderzeichen.
    /// </summary>
    public static void SetColoredPassword(Microsoft.UI.Xaml.Controls.TextBlock target, string password)
    {
        target.Inlines.Clear();
        var digit = LookupThemeBrush(target, "DigitBrush");
        var symbol = LookupThemeBrush(target, "SymbolBrush");

        Run? current = null;
        int currentKind = -1;
        foreach (var ch in password)
        {
            int kind = char.IsDigit(ch) ? 1 : char.IsLetter(ch) ? 0 : 2;
            if (current == null || kind != currentKind)
            {
                current = new Run();
                if (kind == 1) current.Foreground = digit;
                if (kind == 2) current.Foreground = symbol;
                target.Inlines.Add(current);
                currentKind = kind;
            }
            current.Text += ch;
        }
    }

    private static Brush LookupThemeBrush(FrameworkElement element, string key)
    {
        var dark = element.ActualTheme == ElementTheme.Dark;
        var dicts = Application.Current.Resources.ThemeDictionaries;
        if (dicts.TryGetValue(dark ? "Default" : "Light", out var dict)
            && dict is ResourceDictionary rd && rd.TryGetValue(key, out var value) && value is Brush brush)
            return brush;
        return new SolidColorBrush(Colors.Gray);
    }
}

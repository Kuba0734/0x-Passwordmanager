using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public sealed class SecurityReport
{
    public int Total { get; init; }
    public int Score { get; init; }
    public List<PasswordEntry> Weak { get; init; } = new();
    public List<PasswordEntry> Reused { get; init; } = new();
    public List<PasswordEntry> Old { get; init; } = new();
    public List<PasswordEntry> Empty { get; init; } = new();
    public List<PasswordEntry> Insecure { get; init; } = new();
}

/// <summary>
/// Sicherheitscheck: schwache, mehrfach verwendete, alte und leere Passwörter.
/// </summary>
public static class SecurityAnalyzer
{
    public static readonly TimeSpan OldThreshold = TimeSpan.FromDays(365);

    public static SecurityReport Analyze(IReadOnlyCollection<PasswordEntry> entries, DateTime? now = null)
    {
        var reference = now ?? DateTime.UtcNow;
        // Kartennummern sind keine Passwörter → nicht auf Stärke/Wiederverwendung prüfen
        var withPassword = entries.Where(e => !string.IsNullOrEmpty(e.Password) && e.Category != Categories.Cards).ToList();

        var weak = withPassword
            .Where(e => PasswordStrength.Evaluate(e.Password).Level <= StrengthLevel.Weak)
            .ToList();

        var reused = withPassword
            .GroupBy(e => e.Password)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        var old = withPassword
            .Where(e => reference - e.ModifiedAt > OldThreshold)
            .ToList();

        var empty = entries
            .Where(e => string.IsNullOrEmpty(e.Password) && e.Category != Categories.Notes)
            .ToList();

        var insecure = entries
            .Where(e => e.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Score: Anteil der Einträge ohne Probleme, Wiederverwendung wiegt schwer
        int score = 100;
        if (withPassword.Count > 0)
        {
            double penalty =
                weak.Count * 1.0 +
                reused.Count * 1.0 +
                old.Count * 0.35 +
                insecure.Count * 0.25;
            score = (int)Math.Round(100 * Math.Max(0, 1 - penalty / withPassword.Count));
        }

        return new SecurityReport
        {
            Total = entries.Count,
            Score = Math.Clamp(score, 0, 100),
            Weak = weak,
            Reused = reused,
            Old = old,
            Empty = empty,
            Insecure = insecure
        };
    }
}

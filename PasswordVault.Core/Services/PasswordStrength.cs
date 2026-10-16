namespace PasswordVault.Core.Services;

public enum StrengthLevel { None, VeryWeak, Weak, Medium, Strong, VeryStrong }

public readonly record struct StrengthResult(int Score, StrengthLevel Level, double EntropyBits)
{
    public string Label => Level switch
    {
        StrengthLevel.None => "Kein Passwort",
        StrengthLevel.VeryWeak => "Sehr schwach",
        StrengthLevel.Weak => "Schwach",
        StrengthLevel.Medium => "Mittel",
        StrengthLevel.Strong => "Stark",
        _ => "Sehr stark"
    };
}

/// <summary>
/// Schätzt die Passwortstärke über die Zeichenraum-Entropie,
/// mit Abzügen für Wiederholungen, Sequenzen und bekannte Muster.
/// </summary>
public static class PasswordStrength
{
    private static readonly string[] CommonFragments =
    {
        "password", "passwort", "123456", "qwertz", "qwerty", "admin",
        "welcome", "letmein", "hallo", "iloveyou", "abc123", "111111"
    };

    public static StrengthResult Evaluate(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return new StrengthResult(0, StrengthLevel.None, 0);

        int pool = 0;
        if (password.Any(char.IsLower)) pool += 26;
        if (password.Any(char.IsUpper)) pool += 26;
        if (password.Any(char.IsDigit)) pool += 10;
        if (password.Any(c => !char.IsLetterOrDigit(c))) pool += 32;

        // Effektive Länge: Wiederholungen und Sequenzen zählen weniger
        double effectiveLength = 0;
        for (int i = 0; i < password.Length; i++)
        {
            if (i > 0 && password[i] == password[i - 1]) effectiveLength += 0.3;
            else if (i > 0 && Math.Abs(password[i] - password[i - 1]) == 1) effectiveLength += 0.5;
            else effectiveLength += 1;
        }

        double entropy = effectiveLength * Math.Log2(Math.Max(pool, 1));

        var lower = password.ToLowerInvariant();
        if (CommonFragments.Any(lower.Contains))
            entropy *= 0.4;

        int score = (int)Math.Clamp(entropy / 100.0 * 100, 0, 100);
        var level = entropy switch
        {
            < 28 => StrengthLevel.VeryWeak,
            < 40 => StrengthLevel.Weak,
            < 60 => StrengthLevel.Medium,
            < 90 => StrengthLevel.Strong,
            _ => StrengthLevel.VeryStrong
        };

        return new StrengthResult(score, level, entropy);
    }

    /// <summary>
    /// Grobe Schätzung der Knackzeit bei 10 Mrd. Versuchen pro Sekunde (Offline-Angriff).
    /// </summary>
    public static string EstimateCrackTime(double entropyBits)
    {
        if (entropyBits <= 0) return "sofort";
        double seconds = Math.Pow(2, entropyBits - 1) / 1e10;
        return seconds switch
        {
            < 1 => "sofort",
            < 60 => $"{seconds:0} Sekunden",
            < 3600 => $"{seconds / 60:0} Minuten",
            < 86400 => $"{seconds / 3600:0} Stunden",
            < 31_557_600 => $"{seconds / 86400:0} Tage",
            < 31_557_600e3 => $"{seconds / 31_557_600:0} Jahre",
            < 31_557_600e6 => $"{seconds / 31_557_600e3:0} Tsd. Jahre",
            < 31_557_600e9 => $"{seconds / 31_557_600e6:0} Mio. Jahre",
            _ => "Jahrmilliarden"
        };
    }
}

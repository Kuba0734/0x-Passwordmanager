using System.Security.Cryptography;

namespace PasswordVault.Core.Services;

public class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    private const string Ambiguous = "Il1O0o|";

    /// <summary>
    /// Erzeugt ein kryptografisch zufälliges Passwort.
    /// Aus jeder aktivierten Zeichengruppe ist mindestens ein Zeichen enthalten.
    /// </summary>
    public string Generate(
        int length = 20,
        bool useLowercase = true,
        bool useUppercase = true,
        bool useDigits = true,
        bool useSymbols = true,
        bool excludeAmbiguous = false)
    {
        var groups = new List<string>();
        if (useLowercase) groups.Add(Lowercase);
        if (useUppercase) groups.Add(Uppercase);
        if (useDigits) groups.Add(Digits);
        if (useSymbols) groups.Add(Symbols);

        if (groups.Count == 0)
            throw new ArgumentException(
                "Mindestens eine Zeichengruppe aktivieren.");

        if (excludeAmbiguous)
            groups = groups
                .Select(g => new string(g.Where(c => !Ambiguous.Contains(c)).ToArray()))
                .ToList();

        length = Math.Clamp(length, groups.Count, 256);
        var pool = string.Concat(groups);
        var password = new char[length];

        // Je ein Pflichtzeichen pro Gruppe, Rest aus dem Gesamtpool
        for (int i = 0; i < groups.Count; i++)
            password[i] = groups[i][RandomNumberGenerator.GetInt32(groups[i].Length)];
        for (int i = groups.Count; i < length; i++)
            password[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];

        // Fisher-Yates, damit die Pflichtzeichen nicht vorne stehen
        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}

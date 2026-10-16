using System.Security.Cryptography;

namespace PasswordVault.Core.Services;

public class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    public string Generate(
        int length = 20,
        bool useLowercase = true,
        bool useUppercase = true,
        bool useDigits = true,
        bool useSymbols = true)
    {
        var charPool = string.Empty;
        if (useLowercase) charPool += Lowercase;
        if (useUppercase) charPool += Uppercase;
        if (useDigits) charPool += Digits;
        if (useSymbols) charPool += Symbols;

        if (string.IsNullOrEmpty(charPool))
            throw new ArgumentException(
                "Mindestens eine Zeichengruppe aktivieren.");

        var password = new char[length];
        for (int i = 0; i < length; i++)
        {
            password[i] = charPool[
                RandomNumberGenerator.GetInt32(charPool.Length)];
        }

        return new string(password);
    }
}

namespace PasswordVault.Core.Services;

/// <summary>Formate des Update-Systems (Git-Tags und .sha256-Dateien aus deploy.ps1).</summary>
public static class UpdateFormat
{
    /// <summary>"v1.2.3" → 1.2.3; null bei anderem Format.</summary>
    public static Version? ParseTag(string tag)
    {
        var parts = tag.TrimStart('v', 'V').Split('.');
        if (parts.Length != 3 || !parts.All(p => p.Length > 0 && p.All(char.IsAsciiDigit))) return null;
        return new Version(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    /// <summary>Erstes Wort der .sha256-Datei ("HASH  Dateiname"), muss 64 Hex-Zeichen haben.</summary>
    public static string ParseSha256(string content)
    {
        var hash = content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Die Prüfsummen-Datei ist ungültig.");
        return hash;
    }
}

using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class CsvService
{
    /// <summary>
    /// Exportiert Einträge als CSV (Chrome-kompatibel).
    /// </summary>
    public string Export(List<PasswordEntry> entries)
    {
        var lines = new List<string>
        {
            "name,url,username,password,note"
        };

        foreach (var e in entries)
        {
            lines.Add(string.Join(",",
                Escape(e.Title),
                Escape(e.Url),
                Escape(e.Username),
                Escape(e.Password),
                Escape(e.Notes)));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Importiert Einträge aus CSV.
    /// Unterstützt Chrome/Firefox/Bitwarden-Format.
    /// </summary>
    public List<PasswordEntry> Import(string csv)
    {
        var entries = new List<PasswordEntry>();
        var lines = csv.Split('\n',
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2) return entries;

        // Header analysieren
        var header = ParseCsvLine(lines[0]);
        var nameIdx = FindIndex(header,
            "name", "title", "login_name");
        var urlIdx = FindIndex(header,
            "url", "login_uri", "website");
        var userIdx = FindIndex(header,
            "username", "login_username", "user");
        var pwIdx = FindIndex(header,
            "password", "login_password");
        var noteIdx = FindIndex(header,
            "note", "notes", "comments");

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            if (cols.Length == 0) continue;

            entries.Add(new PasswordEntry
            {
                Title = GetCol(cols, nameIdx),
                Url = GetCol(cols, urlIdx),
                Username = GetCol(cols, userIdx),
                Password = GetCol(cols, pwIdx),
                Notes = GetCol(cols, noteIdx),
                Category = "Logins"
            });
        }

        return entries;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"')
            || value.Contains('\n'))
            return $"\"{value.Replace("\"",
                "\"\"")}\"";
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else current += c;
        }
        result.Add(current.Trim());
        return result.ToArray();
    }

    private static int FindIndex(
        string[] header, params string[] names)
    {
        for (int i = 0; i < header.Length; i++)
        {
            var h = header[i].ToLower().Trim();
            if (names.Any(n => h.Contains(n)))
                return i;
        }
        return -1;
    }

    private static string GetCol(
        string[] cols, int idx)
    {
        return idx >= 0 && idx < cols.Length
            ? cols[idx] : "";
    }
}

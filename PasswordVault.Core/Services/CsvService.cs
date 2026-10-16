using System.Text;
using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class CsvService
{
    /// <summary>
    /// Exportiert Einträge als CSV (Chrome-kompatibel, plus Kategorie/Favorit).
    /// </summary>
    public string Export(IEnumerable<PasswordEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append("name,url,username,password,note,category,favorite\r\n");

        foreach (var e in entries)
        {
            sb.Append(string.Join(",",
                Escape(e.Title),
                Escape(e.Url),
                Escape(e.Username),
                Escape(e.Password),
                Escape(e.Notes),
                Escape(e.Category),
                e.IsFavorite ? "1" : "0"));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Importiert Einträge aus CSV.
    /// Unterstützt Chrome/Edge, Firefox, Bitwarden, 1Password, KeePass und den eigenen Export.
    /// </summary>
    public List<PasswordEntry> Import(string csv)
    {
        var entries = new List<PasswordEntry>();
        var rows = ParseCsv(csv.TrimStart('﻿'));
        if (rows.Count < 2) return entries;

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var nameIdx = FindIndex(header, "name", "title", "account");
        var urlIdx = FindIndex(header, "url", "login_uri", "website", "web site", "uri");
        var userIdx = FindIndex(header, "username", "login_username", "user", "user name", "login name", "login", "email");
        var pwIdx = FindIndex(header, "password", "login_password");
        var noteIdx = FindIndex(header, "note", "notes", "comments", "extra");
        var catIdx = FindIndex(header, "category", "folder", "group");
        var favIdx = FindIndex(header, "favorite", "favourite");

        if (pwIdx < 0 && userIdx < 0) return entries;

        for (int i = 1; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.All(string.IsNullOrWhiteSpace)) continue;

            var url = GetCol(cols, urlIdx);
            var title = GetCol(cols, nameIdx);
            if (string.IsNullOrWhiteSpace(title))
                title = UrlMatcher.GetHost(url) ?? GetCol(cols, userIdx);
            if (string.IsNullOrWhiteSpace(title))
                title = "Importierter Eintrag";

            var category = GetCol(cols, catIdx);
            if (!Categories.All.Contains(category))
                category = Categories.Logins;

            var fav = GetCol(cols, favIdx).ToLowerInvariant();

            entries.Add(new PasswordEntry
            {
                Title = title,
                Url = url,
                Username = GetCol(cols, userIdx),
                Password = GetCol(cols, pwIdx),
                Notes = GetCol(cols, noteIdx),
                Category = category,
                IsFavorite = fav is "1" or "true" or "yes" or "ja"
            });
        }

        return entries;
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            || value.StartsWith(' ') || value.EndsWith(' '))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// RFC-4180-Parser: Anführungszeichen, "" als Escape, Zeilenumbrüche in Feldern.
    /// </summary>
    internal static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// Exakte Spaltennamen haben Vorrang vor Teiltreffern
    /// (sonst würde z.B. "username" als "name" erkannt).
    /// </summary>
    private static int FindIndex(string[] header, params string[] names)
    {
        foreach (var n in names)
        {
            var idx = Array.IndexOf(header, n);
            if (idx >= 0) return idx;
        }
        return -1;
    }

    private static string GetCol(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length ? cols[idx].Trim() : "";
}

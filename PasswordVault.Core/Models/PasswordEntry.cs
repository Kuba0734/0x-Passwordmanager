namespace PasswordVault.Core.Models;

public class PasswordEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Category { get; set; } = Categories.General;
    public bool IsFavorite { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public PasswordEntry Clone() => (PasswordEntry)MemberwiseClone();

    // Wird u.a. von Screenreadern für Listeneinträge vorgelesen
    public override string ToString() => Title;
}

public static class Categories
{
    public const string General = "Allgemein";
    public const string Logins = "Logins";
    public const string Cards = "Kreditkarten";
    public const string Notes = "Notizen";

    public static readonly string[] All = { General, Logins, Cards, Notes };
}

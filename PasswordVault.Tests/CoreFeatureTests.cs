using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.Tests;

public class VaultStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pv-tests-" + Guid.NewGuid());
    private string VaultPath => Path.Combine(_dir, "vault.dat");

    public VaultStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Create_Add_Lock_Unlock_PersistsEntries()
    {
        var store = new VaultStore(VaultPath);
        store.Create("Master!Passwort1");
        store.Add(new PasswordEntry { Title = "GitHub", Password = "abc" });
        store.Lock();

        var reopened = new VaultStore(VaultPath);
        reopened.Unlock("Master!Passwort1");
        var entries = reopened.GetEntries();

        Assert.Single(entries);
        Assert.Equal("GitHub", entries[0].Title);
    }

    [Fact]
    public void Unlock_WrongPassword_Throws_AndStaysLocked()
    {
        var store = new VaultStore(VaultPath);
        store.Create("richtig");
        store.Lock();

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => store.Unlock("falsch"));
        Assert.False(store.IsUnlocked);
    }

    [Fact]
    public void Update_And_Delete_ArePersisted()
    {
        var store = new VaultStore(VaultPath);
        store.Create("pw");
        var entry = new PasswordEntry { Title = "Alt" };
        store.Add(entry);

        var edited = store.Get(entry.Id)!;
        edited.Title = "Neu";
        store.Update(edited);
        Assert.Equal("Neu", store.Get(entry.Id)!.Title);

        Assert.True(store.Delete(entry.Id));
        Assert.Empty(store.GetEntries());
    }

    [Fact]
    public void GetEntries_ReturnsCopies()
    {
        var store = new VaultStore(VaultPath);
        store.Create("pw");
        store.Add(new PasswordEntry { Title = "Original" });

        store.GetEntries()[0].Title = "Manipuliert";

        Assert.Equal("Original", store.GetEntries()[0].Title);
    }

    [Fact]
    public void ChangeMasterPassword_OldFails_NewWorks()
    {
        var store = new VaultStore(VaultPath);
        store.Create("alt");
        store.Add(new PasswordEntry { Title = "X" });
        store.ChangeMasterPassword("neu");
        store.Lock();

        Assert.False(store.VerifyPassword("alt"));
        Assert.True(store.VerifyPassword("neu"));
        store.Unlock("neu");
        Assert.Single(store.GetEntries());
    }

    [Fact]
    public void FindForUrl_MatchesSubdomains_BestFirst()
    {
        var store = new VaultStore(VaultPath);
        store.Create("pw");
        store.AddRange(new[]
        {
            new PasswordEntry { Title = "Google", Url = "google.com" },
            new PasswordEntry { Title = "Accounts", Url = "https://accounts.google.com" },
            new PasswordEntry { Title = "Andere", Url = "https://example.org" }
        });

        var matches = store.FindForUrl("https://accounts.google.com/signin");

        Assert.Equal(new[] { "Accounts", "Google" }, matches.Select(m => m.Title));
    }

    [Fact]
    public void Changed_IsRaised_WithSource()
    {
        var store = new VaultStore(VaultPath);
        store.Create("pw");
        VaultChangeSource? seen = null;
        store.Changed += (_, e) => seen = e.Source;

        store.Add(new PasswordEntry { Title = "A" }, VaultChangeSource.Browser);

        Assert.Equal(VaultChangeSource.Browser, seen);
    }
}

public class CsvServiceTests
{
    [Fact]
    public void Export_Import_Roundtrip_WithSpecialCharacters()
    {
        var csv = new CsvService();
        var original = new List<PasswordEntry>
        {
            new() { Title = "Komma, \"Quote\"", Username = "u", Password = "p,\"x\"",
                    Notes = "Zeile1\nZeile2", Url = "https://a.de",
                    Category = Categories.Cards, IsFavorite = true, Expiry = "12/29", Cvc = "123" }
        };

        var imported = csv.Import(csv.Export(original));

        Assert.Single(imported);
        Assert.Equal(original[0].Title, imported[0].Title);
        Assert.Equal(original[0].Password, imported[0].Password);
        Assert.Equal(original[0].Notes, imported[0].Notes);
        Assert.Equal(Categories.Cards, imported[0].Category);
        Assert.True(imported[0].IsFavorite);
        Assert.Equal("12/29", imported[0].Expiry);
        Assert.Equal("123", imported[0].Cvc);
    }

    [Fact]
    public void Import_Firefox_UsesHostAsTitle_NotUsername()
    {
        var text = "\"url\",\"username\",\"password\",\"httpRealm\"\n" +
                   "\"https://www.github.com\",\"octo\",\"pw1\",\"\"\n";

        var entries = new CsvService().Import(text);

        Assert.Single(entries);
        Assert.Equal("github.com", entries[0].Title);
        Assert.Equal("octo", entries[0].Username);
    }

    [Fact]
    public void Import_Bitwarden_Format()
    {
        var text = "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n" +
                   ",1,login,Steam,,,,https://store.steampowered.com,gamer,s3cret,\n";

        var e = Assert.Single(new CsvService().Import(text));

        Assert.Equal("Steam", e.Title);
        Assert.Equal("gamer", e.Username);
        Assert.Equal("s3cret", e.Password);
        Assert.True(e.IsFavorite);
    }
}

public class PasswordToolsTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void Generator_ContainsEveryEnabledGroup(bool lower, bool upper, bool digits, bool symbols)
    {
        var gen = new PasswordGenerator();
        for (int i = 0; i < 50; i++)
        {
            var pw = gen.Generate(8, lower, upper, digits, symbols);
            Assert.Equal(8, pw.Length);
            Assert.Equal(lower, pw.Any(char.IsLower));
            Assert.Equal(upper, pw.Any(char.IsUpper));
            Assert.Equal(digits, pw.Any(char.IsDigit));
            Assert.Equal(symbols, pw.Any(c => !char.IsLetterOrDigit(c)));
        }
    }

    [Fact]
    public void Generator_ExcludeAmbiguous()
    {
        var pw = new PasswordGenerator().Generate(200, excludeAmbiguous: true);
        Assert.DoesNotContain(pw, c => "Il1O0o|".Contains(c));
    }

    [Fact]
    public void Strength_RanksPasswordsSensibly()
    {
        Assert.Equal(StrengthLevel.None, PasswordStrength.Evaluate("").Level);
        Assert.True(PasswordStrength.Evaluate("passwort123").Level <= StrengthLevel.Weak);
        Assert.True(PasswordStrength.Evaluate("aaaaaaaa").Level <= StrengthLevel.Weak);
        Assert.True(PasswordStrength.Evaluate("hT7#kq9!Lm2$vX4@").Level >= StrengthLevel.Strong);
    }

    [Theory]
    [InlineData("https://www.github.com/login", "github.com", 3)]
    [InlineData("https://gist.github.com", "github.com", 2)]
    [InlineData("https://a.example.co.uk", "https://b.example.co.uk", 1)]
    [InlineData("https://evil-github.com", "github.com", 0)]
    [InlineData("https://co.uk", "https://other.co.uk", 0)]
    [InlineData("javascript:alert(1)", "github.com", 0)]
    public void UrlMatcher_Scores(string page, string entry, int expected)
    {
        Assert.Equal(expected, UrlMatcher.MatchScore(entry, page));
    }

    [Fact]
    public void SecurityAnalyzer_FindsWeakAndReused()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "A", Password = "123456" },
            new() { Title = "B", Password = "hT7#kq9!Lm2$vX4@" },
            new() { Title = "C", Password = "hT7#kq9!Lm2$vX4@" },
            new() { Title = "D", Password = "Zx9!qW3#rT6$yU8%", ModifiedAt = DateTime.UtcNow.AddYears(-2) }
        };

        var report = SecurityAnalyzer.Analyze(entries);

        Assert.Single(report.Weak);
        Assert.Equal(2, report.Reused.Count);
        Assert.Single(report.Old);
        Assert.InRange(report.Score, 1, 99);
    }
}

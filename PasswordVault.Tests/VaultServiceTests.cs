using PasswordVault.Core.Models;
using PasswordVault.Core.Services;

namespace PasswordVault.Tests;

public class VaultServiceTests
{
    [Fact]
    public void CreateVault_ThenEncryptDecrypt_Works()
    {
        var vault = new VaultService();
        var metadata = vault.CreateVault("MasterPasswort!");

        var entries = new List<PasswordEntry>
        {
            new() { Title = "GitHub",
                    Username = "user@mail.de",
                    Password = "geheim123" },
            new() { Title = "Steam",
                    Username = "gamer42",
                    Password = "s3cur3!" }
        };

        var encrypted = vault.EncryptEntries(entries);
        var decrypted = vault.DecryptEntries(encrypted);

        Assert.Equal(2, decrypted.Count);
        Assert.Equal("GitHub", decrypted[0].Title);
        Assert.Equal("geheim123", decrypted[0].Password);
    }

    [Fact]
    public void Lock_ThenEncrypt_ThrowsException()
    {
        var vault = new VaultService();
        vault.CreateVault("Passwort");
        vault.Lock();

        Assert.Throws<InvalidOperationException>(
            () => vault.EncryptEntries(new List<PasswordEntry>()));
    }
}

using PasswordVault.Core.Crypto;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System.Text;

namespace PasswordVault.Tests;

public class CryptoTests
{
    [Fact]
    public void DeriveKey_SameInput_SameOutput()
    {
        var service = new KeyDerivationService();
        var salt = service.GenerateSalt();

        var key1 = service.DeriveKey("MeinPasswort123", salt);
        var key2 = service.DeriveKey("MeinPasswort123", salt);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveKey_DifferentPassword_DifferentOutput()
    {
        var service = new KeyDerivationService();
        var salt = service.GenerateSalt();

        var key1 = service.DeriveKey("Passwort1", salt);
        var key2 = service.DeriveKey("Passwort2", salt);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Encrypt_Decrypt_ReturnsOriginal()
    {
        var keyService = new KeyDerivationService();
        var encService = new EncryptionService();
        var salt = keyService.GenerateSalt();
        var key = keyService.DeriveKey("TestPasswort", salt);

        var original = Encoding.UTF8.GetBytes("Geheime Daten!");
        var encrypted = encService.Encrypt(original, key);
        var decrypted = encService.Decrypt(encrypted, key);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsException()
    {
        var keyService = new KeyDerivationService();
        var encService = new EncryptionService();

        var key1 = keyService.DeriveKey("Richtig",
            keyService.GenerateSalt());
        var key2 = keyService.DeriveKey("Falsch",
            keyService.GenerateSalt());

        var encrypted = encService.Encrypt(
            Encoding.UTF8.GetBytes("Daten"), key1);

        Assert.ThrowsAny<Exception>(
            () => encService.Decrypt(encrypted, key2));
    }
}

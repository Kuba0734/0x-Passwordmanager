using System.Text;
using System.Text.Json;
using PasswordVault.Core.Crypto;
using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class VaultService
{
    private readonly KeyDerivationService _keyService = new();
    private readonly EncryptionService _encryptionService = new();

    private byte[]? _derivedKey;
    private VaultMetadata? _metadata;

    public bool IsUnlocked => _derivedKey != null;

    /// <summary>
    /// Erstellt einen neuen Tresor mit dem Master-Passwort.
    /// </summary>
    public VaultMetadata CreateVault(string masterPassword)
    {
        var salt = _keyService.GenerateSalt();
        _metadata = new VaultMetadata { Salt = salt };
        _derivedKey = _keyService.DeriveKey(
            masterPassword, salt,
            _metadata.Argon2Iterations,
            _metadata.Argon2MemorySize,
            _metadata.Argon2Parallelism);
        return _metadata;
    }

    /// <summary>
    /// Entsperrt einen bestehenden Tresor.
    /// </summary>
    public void Unlock(string masterPassword, VaultMetadata metadata)
    {
        _metadata = metadata;
        _derivedKey = _keyService.DeriveKey(
            masterPassword, metadata.Salt,
            metadata.Argon2Iterations,
            metadata.Argon2MemorySize,
            metadata.Argon2Parallelism);
    }

    /// <summary>
    /// Verschlüsselt eine Liste von Einträgen.
    /// </summary>
    public byte[] EncryptEntries(List<PasswordEntry> entries)
    {
        if (_derivedKey == null)
            throw new InvalidOperationException("Vault ist gesperrt.");

        var json = JsonSerializer.Serialize(entries, VaultJsonContext.Default.ListPasswordEntry);
        var plaintext = Encoding.UTF8.GetBytes(json);
        return _encryptionService.Encrypt(plaintext, _derivedKey);
    }

    /// <summary>
    /// Entschlüsselt Bytes zurück zu Einträgen.
    /// </summary>
    public List<PasswordEntry> DecryptEntries(byte[] encryptedData)
    {
        if (_derivedKey == null)
            throw new InvalidOperationException("Vault ist gesperrt.");

        var plaintext = _encryptionService.Decrypt(encryptedData, _derivedKey);
        var json = Encoding.UTF8.GetString(plaintext);
        return JsonSerializer.Deserialize(json, VaultJsonContext.Default.ListPasswordEntry)
               ?? new List<PasswordEntry>();
    }

    /// <summary>
    /// Sperrt den Tresor (löscht den Schlüssel aus dem Speicher).
    /// </summary>
    public void Lock()
    {
        if (_derivedKey != null)
        {
            Array.Clear(_derivedKey);
            _derivedKey = null;
        }
    }

    /// <summary>
    /// Gibt den aktuellen Salt zurück (für Metadaten-Speicherung).
    /// </summary>
    public byte[] GetSalt()
    {
        return _metadata?.Salt ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Erzeugt neuen Salt und Schlüssel für ein neues Master-Passwort.
    /// Der Aufrufer muss die Einträge anschließend neu verschlüsseln und speichern.
    /// </summary>
    public VaultMetadata Rekey(string newPassword)
    {
        if (_derivedKey == null)
            throw new InvalidOperationException("Vault ist gesperrt.");

        var newMetadata = new VaultMetadata
        {
            Salt = _keyService.GenerateSalt(),
            CreatedAt = _metadata?.CreatedAt ?? DateTime.UtcNow
        };
        var newKey = _keyService.DeriveKey(
            newPassword, newMetadata.Salt,
            newMetadata.Argon2Iterations,
            newMetadata.Argon2MemorySize,
            newMetadata.Argon2Parallelism);

        Array.Clear(_derivedKey);
        _derivedKey = newKey;
        _metadata = newMetadata;
        return newMetadata;
    }

    /// <summary>
    /// Ändert das Master-Passwort.
    /// Entschlüsselt mit altem Key, generiert neuen Salt,
    /// leitet neuen Key ab, verschlüsselt neu.
    /// </summary>
    public (VaultMetadata NewMetadata, byte[] NewEncrypted)
        ChangePassword(
        string newPassword,
        byte[] currentEncryptedData)
    {
        if (_derivedKey == null)
            throw new InvalidOperationException(
                "Vault ist gesperrt.");

        // 1. Aktuelle Daten entschlüsseln
        var plaintext = _encryptionService.Decrypt(
            currentEncryptedData, _derivedKey);

        // 2. Neuen Salt + Key generieren
        var newSalt = _keyService.GenerateSalt();
        var newMetadata = new VaultMetadata { Salt = newSalt };
        _derivedKey = _keyService.DeriveKey(
            newPassword, newSalt,
            newMetadata.Argon2Iterations,
            newMetadata.Argon2MemorySize,
            newMetadata.Argon2Parallelism);
        _metadata = newMetadata;

        // 3. Mit neuem Key verschlüsseln
        var newEncrypted = _encryptionService.Encrypt(
            plaintext, _derivedKey);

        return (newMetadata, newEncrypted);
    }


}

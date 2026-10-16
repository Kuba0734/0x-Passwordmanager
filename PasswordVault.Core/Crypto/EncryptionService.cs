using System.Security.Cryptography;

namespace PasswordVault.Core.Crypto;

public class EncryptionService
{
    private const int NonceSize = 12;  // 96 Bit (GCM-Standard)
    private const int TagSize = 16;     // 128 Bit Auth-Tag

    /// <summary>
    /// Verschlüsselt Klartext-Bytes mit AES-256-GCM.
    /// Rückgabe: Nonce + Ciphertext + AuthTag
    /// </summary>
    public byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Alles zusammenpacken: Nonce | Ciphertext | Tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NonceSize);
        tag.CopyTo(result, NonceSize + ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Entschlüsselt AES-256-GCM-Daten.
    /// Erwartet: Nonce + Ciphertext + AuthTag
    /// </summary>
    public byte[] Decrypt(byte[] encryptedData, byte[] key)
    {
        var nonce = encryptedData[..NonceSize];
        var tag = encryptedData[^TagSize..];
        var ciphertext = encryptedData[NonceSize..^TagSize];

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}

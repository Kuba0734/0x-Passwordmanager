using System.Text.Json;
using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class VaultFileService
{
    /// <summary>
    /// Speichert Metadaten + verschlüsselte Daten in eine Datei.
    /// Format: [4 Bytes MetaLength][MetaJSON][EncryptedData]
    /// </summary>
    public void Save(
        string filePath,
        VaultMetadata metadata,
        byte[] encryptedData)
    {
        var metaJson = JsonSerializer.Serialize(metadata, VaultJsonContext.Default.VaultMetadata);
        var metaBytes = System.Text.Encoding.UTF8.GetBytes(metaJson);

        using var stream = File.Create(filePath);
        // Länge der Metadaten als 4 Bytes
        stream.Write(BitConverter.GetBytes(metaBytes.Length));
        // Metadaten als JSON
        stream.Write(metaBytes);
        // Verschlüsselte Daten
        stream.Write(encryptedData);
    }

    /// <summary>
    /// Lädt Metadaten + verschlüsselte Daten aus einer Datei.
    /// </summary>
    public (VaultMetadata Metadata, byte[] EncryptedData) Load(
        string filePath)
    {
        var fileBytes = File.ReadAllBytes(filePath);

        // Erste 4 Bytes = Länge der Metadaten
        var metaLength = BitConverter.ToInt32(fileBytes, 0);

        // Metadaten extrahieren
        var metaJson = System.Text.Encoding.UTF8.GetString(
            fileBytes, 4, metaLength);
        var metadata = JsonSerializer.Deserialize(metaJson, VaultJsonContext.Default.VaultMetadata) ?? new VaultMetadata();


        // Verschlüsselte Daten extrahieren
        var dataStart = 4 + metaLength;
        var encryptedData = fileBytes[dataStart..];

        return (metadata, encryptedData);
    }

    /// <summary>
    /// Prüft ob eine Vault-Datei existiert.
    /// </summary>
    public bool Exists(string filePath)
    {
        return File.Exists(filePath);
    }
}

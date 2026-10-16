using System.Text.Json;
using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class VaultFileService
{
    /// <summary>
    /// Speichert Metadaten + verschlüsselte Daten in eine Datei.
    /// Format: [4 Bytes MetaLength][MetaJSON][EncryptedData]
    /// Geschrieben wird atomar über eine Temp-Datei, damit ein Absturz
    /// mitten im Speichern den Tresor nicht beschädigt.
    /// </summary>
    public void Save(
        string filePath,
        VaultMetadata metadata,
        byte[] encryptedData)
    {
        var metaJson = JsonSerializer.Serialize(metadata, VaultJsonContext.Default.VaultMetadata);
        var metaBytes = System.Text.Encoding.UTF8.GetBytes(metaJson);

        var tempPath = filePath + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            stream.Write(BitConverter.GetBytes(metaBytes.Length));
            stream.Write(metaBytes);
            stream.Write(encryptedData);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(filePath))
            File.Replace(tempPath, filePath, filePath + ".bak", ignoreMetadataErrors: true);
        else
            File.Move(tempPath, filePath);
    }

    /// <summary>
    /// Lädt Metadaten + verschlüsselte Daten aus einer Datei.
    /// </summary>
    public (VaultMetadata Metadata, byte[] EncryptedData) Load(
        string filePath)
    {
        var fileBytes = File.ReadAllBytes(filePath);
        if (fileBytes.Length < 4)
            throw new InvalidDataException("Tresor-Datei ist beschädigt.");

        var metaLength = BitConverter.ToInt32(fileBytes, 0);
        if (metaLength <= 0 || 4 + metaLength > fileBytes.Length)
            throw new InvalidDataException("Tresor-Datei ist beschädigt.");

        var metaJson = System.Text.Encoding.UTF8.GetString(fileBytes, 4, metaLength);
        var metadata = JsonSerializer.Deserialize(metaJson, VaultJsonContext.Default.VaultMetadata)
            ?? new VaultMetadata();

        var encryptedData = fileBytes[(4 + metaLength)..];
        return (metadata, encryptedData);
    }

    public bool Exists(string filePath) => File.Exists(filePath);
}

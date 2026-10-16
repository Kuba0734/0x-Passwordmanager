namespace PasswordVault.Core.Models;

public class VaultMetadata
{
    public string Version { get; set; } = "1.0";
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public int Argon2Iterations { get; set; } = 4;
    public int Argon2MemorySize { get; set; } = 65536; // 64 MB
    public int Argon2Parallelism { get; set; } = 2;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastOpenedAt { get; set; } = DateTime.UtcNow;
}
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace PasswordVault.Core.Crypto;

public class KeyDerivationService
{
    /// <summary>
    /// Erzeugt einen zufälligen Salt (16 Bytes).
    /// </summary>
    public byte[] GenerateSalt()
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    /// <summary>
    /// Leitet aus dem Master-Passwort einen 256-Bit-Schlüssel ab.
    /// </summary>
    public byte[] DeriveKey(
        string masterPassword,
        byte[] salt,
        int iterations = 4,
        int memorySize = 65536,
        int parallelism = 2)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);

        var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            Iterations = iterations,
            MemorySize = memorySize,    // in KB (65536 = 64 MB)
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(32); // 32 Bytes = 256 Bit
    }
}

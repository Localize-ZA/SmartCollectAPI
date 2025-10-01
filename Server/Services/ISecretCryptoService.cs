using System.Security.Cryptography;

namespace SmartCollectAPI.Services;

public record SecretCipher(byte[] Ciphertext, byte[] Iv, byte[] Tag, int Version);

public interface ISecretCryptoService
{
    SecretCipher Encrypt(string plaintext);
    string Decrypt(byte[] ciphertext, byte[] iv, byte[] tag, int version);
    int CurrentVersion { get; }
}


using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace SmartCollectAPI.Services;

public class SecretCryptoService : ISecretCryptoService
{
    private readonly byte[] _masterKey; // 32 bytes
    private readonly int _version;
    private readonly ILogger<SecretCryptoService> _logger;

    public int CurrentVersion => _version;

    public SecretCryptoService(IConfiguration configuration, ILogger<SecretCryptoService> logger, IHostEnvironment env)
    {
        _logger = logger;

        var base64 = Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")
                    ?? configuration["APP_ENCRYPTION_KEY"];

        if (string.IsNullOrWhiteSpace(base64))
        {
            if (!env.IsDevelopment())
            {
                throw new InvalidOperationException("APP_ENCRYPTION_KEY is not configured.");
            }
            // Development fallback: generate ephemeral key (not persisted)
            _logger.LogWarning("APP_ENCRYPTION_KEY missing; generating ephemeral development key");
            _masterKey = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            try
            {
                _masterKey = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("APP_ENCRYPTION_KEY must be Base64-encoded");
            }
        }

        if (_masterKey.Length != 32)
        {
            throw new InvalidOperationException("APP_ENCRYPTION_KEY must decode to 32 bytes for AES-256-GCM");
        }

        // Versioning: start at 1; future rotation can load multiple versions
        _version = 1;
    }

    public SecretCipher Encrypt(string plaintext)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
        byte[]? pt = null;
        byte[]? ct = null;
        byte[]? iv = null;
        byte[]? tag = null;

        try
        {
            pt = Encoding.UTF8.GetBytes(plaintext);
            ct = new byte[pt.Length];
            iv = RandomNumberGenerator.GetBytes(12); // 96-bit nonce recommended for GCM
            tag = new byte[16]; // 128-bit tag

            using var aes = new AesGcm(_masterKey, 16);
            aes.Encrypt(iv, pt, ct, tag);

            return new SecretCipher(ct, iv, tag, _version);
        }
        finally
        {
            if (pt != null) CryptographicOperations.ZeroMemory(pt);
        }
    }

    public string Decrypt(byte[] ciphertext, byte[] iv, byte[] tag, int version)
    {
        // For rotation support, version dispatch could be added here.
        // Currently we support only the current master key version.
        if (ciphertext == null) throw new ArgumentNullException(nameof(ciphertext));
        if (iv == null) throw new ArgumentNullException(nameof(iv));
        if (tag == null) throw new ArgumentNullException(nameof(tag));

        byte[]? pt = null;
        try
        {
            pt = new byte[ciphertext.Length];
            using var aes = new AesGcm(_masterKey, 16);
            aes.Decrypt(iv, ciphertext, tag, pt);
            return Encoding.UTF8.GetString(pt);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Secret decryption failed (version {Version})", version);
            throw;
        }
        finally
        {
            if (pt != null) CryptographicOperations.ZeroMemory(pt);
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryption for sensitive values (AI API keys).
/// Stored format: "v2:{nonce_b64}:{ciphertext_b64}:{tag_b64}"
/// Legacy format (base64 plain text) is decoded transparently for backward compat.
/// </summary>
public class KeyEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<KeyEncryptionService> _logger;

    private const string Prefix = "v2:";

    public KeyEncryptionService(IConfiguration config, ILogger<KeyEncryptionService> logger)
    {
        _logger = logger;
        var rawKey = config["AiKeys:EncryptionKey"] ?? "";

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            _logger.LogWarning("AiKeys:EncryptionKey no configurado — usando clave derivada de host (baja seguridad). Configura una clave de 32+ chars en producción.");
            rawKey = $"AgenteNews-Default-{Environment.MachineName}";
        }

        // Derive a 32-byte AES-256 key via HKDF
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(rawKey),
            outputLength: 32,
            info: "AgenteNews.AiKey.v2"u8.ToArray());
    }

    public string Encrypt(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[data.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, data, ciphertext, tag);

        return $"{Prefix}{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(tag)}";
    }

    public string Decrypt(string stored)
    {
        // New format
        if (stored.StartsWith(Prefix))
        {
            var parts = stored[Prefix.Length..].Split(':');
            if (parts.Length != 3)
                throw new FormatException("Formato de clave cifrada inválido.");

            var nonce = Convert.FromBase64String(parts[0]);
            var ciphertext = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }

        // Legacy base64 format — decode and return as-is (backward compat)
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(stored));
        }
        catch
        {
            return stored; // plain text fallback
        }
    }
}

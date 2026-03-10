using ANews.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANews.Tests.Infrastructure;

public class KeyEncryptionServiceTests
{
    private KeyEncryptionService CreateService(string? key = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(key != null
                ? new[] { KeyValuePair.Create("AiKeys:EncryptionKey", key)! }
                : [])
            .Build();

        return new KeyEncryptionService(config, NullLogger<KeyEncryptionService>.Instance);
    }

    [Fact]
    public void EncryptDecrypt_Roundtrip_Success()
    {
        var svc = CreateService("my-super-secret-key-32-chars-long!");
        var original = "sk-abc123-my-api-key";

        var encrypted = svc.Encrypt(original);
        var decrypted = svc.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
        Assert.NotEqual(original, encrypted);
        Assert.StartsWith("v2:", encrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertexts_ForSamePlaintext()
    {
        var svc = CreateService("test-key-for-uniqueness-check-32!");
        var plaintext = "same-api-key";

        var enc1 = svc.Encrypt(plaintext);
        var enc2 = svc.Encrypt(plaintext);

        // Due to random nonce, ciphertexts should differ
        Assert.NotEqual(enc1, enc2);

        // But both should decrypt to the same value
        Assert.Equal(plaintext, svc.Decrypt(enc1));
        Assert.Equal(plaintext, svc.Decrypt(enc2));
    }

    [Fact]
    public void Decrypt_LegacyBase64_Success()
    {
        var svc = CreateService("any-key");
        var plaintext = "my-legacy-key";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        var decrypted = svc.Decrypt(base64);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_PlainText_Fallback()
    {
        var svc = CreateService("any-key");
        // Not valid base64, not v2: prefix — falls back to returning as-is
        var plaintext = "not-base64-not-v2";

        var decrypted = svc.Decrypt(plaintext);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Service_WithoutKey_UsesDefaultDerivedKey()
    {
        // No key configured — should still work (uses machine name fallback)
        var svc = CreateService(null);
        var original = "test-api-key";

        var encrypted = svc.Encrypt(original);
        var decrypted = svc.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }
}

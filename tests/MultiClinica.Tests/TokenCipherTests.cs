using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MultiClinica.API.Services.MercadoPago;
using Xunit;

namespace MultiClinica.Tests;

public class TokenCipherTests
{
    private static TokenCipher Cipher(string? key = null) => new(Options.Create(new MercadoPagoOptions
    {
        TokenEncryptionKey = key ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    }));

    [Fact]
    public void Encrypt_then_decrypt_returns_original()
    {
        var cipher = Cipher();
        var encrypted = cipher.Encrypt("APP_USR-123");
        Assert.NotEqual("APP_USR-123", encrypted);
        Assert.Equal("APP_USR-123", cipher.Decrypt(encrypted));
    }

    [Fact]
    public void Tampered_text_fails()
    {
        var cipher = Cipher();
        var bytes = Convert.FromBase64String(cipher.Encrypt("APP_USR-123"));
        bytes[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Missing_key_is_not_configured()
        => Assert.False(new TokenCipher(Options.Create(new MercadoPagoOptions())).IsConfigured);
}

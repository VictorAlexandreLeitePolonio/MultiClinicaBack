using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MultiClinica.API.Services.MercadoPago;

// AES-GCM com chave de configuração. Formato: base64(nonce 12 + tag 16 + ciphertext).
public sealed class TokenCipher(IOptions<MercadoPagoOptions> options)
{
    public bool IsConfigured => Key() is not null;

    public string Encrypt(string plain)
    {
        var key = Key() ?? throw new InvalidOperationException("MercadoPago:TokenEncryptionKey não configurada.");
        var data = Encoding.UTF8.GetBytes(plain);
        var output = new byte[12 + 16 + data.Length];
        var nonce = output.AsSpan(0, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, data, output.AsSpan(28), output.AsSpan(12, 16));
        return Convert.ToBase64String(output);
    }

    // Lança CryptographicException se o texto foi adulterado ou a chave mudou.
    public string Decrypt(string encrypted)
    {
        var key = Key() ?? throw new InvalidOperationException("MercadoPago:TokenEncryptionKey não configurada.");
        var input = Convert.FromBase64String(encrypted);
        var plain = new byte[input.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(input.AsSpan(0, 12), input.AsSpan(28), input.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[]? Key()
    {
        var value = options.Value.TokenEncryptionKey;
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = Convert.FromBase64String(value);
        return key.Length == 32 ? key : throw new InvalidOperationException("MercadoPago:TokenEncryptionKey precisa ter 32 bytes.");
    }
}

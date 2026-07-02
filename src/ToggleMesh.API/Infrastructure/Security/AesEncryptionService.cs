using System.Security.Cryptography;
using System.Text;

namespace ToggleMesh.API.Infrastructure.Security;

public class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _masterKey;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Webhooks:MasterKey"];
        if (string.IsNullOrEmpty(keyString))
            throw new InvalidOperationException("Webhooks:MasterKey configuration is missing.");

        _masterKey = Convert.FromBase64String(keyString);
        
        if (_masterKey.Length != 32)
            throw new InvalidOperationException("Webhooks:MasterKey must be a 256-bit (32 byte) key.");
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentNullException(nameof(plaintext));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = new byte[plaintextBytes.Length];
        
        var nonceBytes = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonceBytes);
        
        var tagBytes = new byte[TagSize];

        using var aesGcm = new AesGcm(_masterKey, TagSize);
        aesGcm.Encrypt(nonceBytes, plaintextBytes, ciphertextBytes, tagBytes);

        var resultBytes = new byte[NonceSize + TagSize + ciphertextBytes.Length];
        Buffer.BlockCopy(nonceBytes, 0, resultBytes, 0, NonceSize);
        Buffer.BlockCopy(tagBytes, 0, resultBytes, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertextBytes, 0, resultBytes, NonceSize + TagSize, ciphertextBytes.Length);

        return Convert.ToBase64String(resultBytes);
    }

    public string Decrypt(string ciphertextBase64)
    {
        if (string.IsNullOrEmpty(ciphertextBase64))
            throw new ArgumentNullException(nameof(ciphertextBase64));

        var fullCiphertextBytes = Convert.FromBase64String(ciphertextBase64);

        if (fullCiphertextBytes.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext length.");

        var nonceBytes = new byte[NonceSize];
        var tagBytes = new byte[TagSize];
        var ciphertextBytes = new byte[fullCiphertextBytes.Length - NonceSize - TagSize];
        var plaintextBytes = new byte[ciphertextBytes.Length];

        Buffer.BlockCopy(fullCiphertextBytes, 0, nonceBytes, 0, NonceSize);
        Buffer.BlockCopy(fullCiphertextBytes, NonceSize, tagBytes, 0, TagSize);
        Buffer.BlockCopy(fullCiphertextBytes, NonceSize + TagSize, ciphertextBytes, 0, ciphertextBytes.Length);

        using var aesGcm = new AesGcm(_masterKey, TagSize);
        aesGcm.Decrypt(nonceBytes, ciphertextBytes, tagBytes, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}

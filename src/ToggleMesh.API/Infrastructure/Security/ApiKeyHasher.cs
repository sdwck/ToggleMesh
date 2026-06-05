using System.Security.Cryptography;
using System.Text;

namespace ToggleMesh.API.Infrastructure.Security;

public static class ApiKeyHasher
{
    public static string Hash(string plainKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plainKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GeneratePreview(string plainKey)
    {
        if (plainKey.Length < 12)
            return "...";
        return plainKey[..7] + "..." + plainKey[^4..];
    }
}
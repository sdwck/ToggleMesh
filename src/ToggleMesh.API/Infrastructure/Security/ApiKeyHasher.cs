using System.Security.Cryptography;
using System.Text;

namespace ToggleMesh.API.Infrastructure.Security;

public static class ApiKeyHasher
{
    public static string Pepper { get; set; } = "DefaultToggleMeshPepperSecret123!";

    public static string Hash(string plainKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plainKey);
        var pepperBytes = Encoding.UTF8.GetBytes(Pepper);
        using var hmac = new HMACSHA256(pepperBytes);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GeneratePreview(string plainKey)
    {
        var lastIndex = plainKey.LastIndexOf('_');
        if (lastIndex < 0)
        {
            if (plainKey.Length < 12)
                return "...";
            return plainKey[..7] + "***" + plainKey[^4..];
        }

        var prefix = plainKey[..(lastIndex + 1)];
        var suffixStart = lastIndex + 1;
        var remaining = plainKey[suffixStart..];

        if (remaining.Length < 7)
        {
            return prefix + "***";
        }

        var nextThree = remaining[..3];
        var lastFour = remaining[^4..];
        return $"{prefix}{nextThree}***{lastFour}";
    }
}
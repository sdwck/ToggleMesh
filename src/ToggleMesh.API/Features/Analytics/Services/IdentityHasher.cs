using System.Security.Cryptography;
using System.Text;

namespace ToggleMesh.API.Features.Analytics.Services;

public interface IIdentityHasher
{
    bool IsEnabled { get; }
    string HashIdentity(string rawIdentity);
    string FormatLiveTailIdentity(string rawIdentity);
}

public class IdentityHasher : IIdentityHasher
{
    private readonly byte[] _saltBytes;
    public bool IsEnabled { get; }

    public IdentityHasher(IConfiguration configuration, ILogger<IdentityHasher> logger)
    {
        IsEnabled = configuration.GetValue("Analytics:HashIdentities", true);

        var salt = configuration["Analytics:IdentityHashSalt"];
        if (string.IsNullOrWhiteSpace(salt))
        {
            var keysDir = Path.Combine(AppContext.BaseDirectory, "keys");
            var keyPath = Path.Combine(keysDir, "identity_hash_salt.key");

            if (File.Exists(keyPath))
            {
                salt = File.ReadAllText(keyPath).Trim();
            }
            else
            {
                if (!Directory.Exists(keysDir))
                    Directory.CreateDirectory(keysDir);

                var randomBytes = RandomNumberGenerator.GetBytes(32);
                salt = Convert.ToHexStringLower(randomBytes);
                File.WriteAllText(keyPath, salt);
                logger.LogInformation("[IdentityHasher] Generated and persisted new HMAC salt to {Path}", keyPath);
            }
        }

        _saltBytes = Encoding.UTF8.GetBytes(salt);
    }

    public string HashIdentity(string rawIdentity)
    {
        if (string.IsNullOrEmpty(rawIdentity)) return string.Empty;
        if (!IsEnabled) return rawIdentity;

        using var hmac = new HMACSHA256(_saltBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawIdentity));
        return Convert.ToHexStringLower(hashBytes)[..32];
    }

    public string FormatLiveTailIdentity(string rawIdentity)
    {
        if (string.IsNullOrEmpty(rawIdentity)) return string.Empty;
        if (!IsEnabled) return rawIdentity;

        var fullHash = HashIdentity(rawIdentity);
        return $"{fullHash[..8]}...";
    }
}

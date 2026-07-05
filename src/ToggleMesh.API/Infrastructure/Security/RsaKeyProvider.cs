using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ToggleMesh.API.Infrastructure.Security;

public static class RsaKeyProvider
{
    private static volatile RsaSecurityKey? _key;
    private static readonly Lock Lock = new();

    public static RsaSecurityKey GetKey(IConfiguration configuration)
    {
        var key = _key;
        if (key != null) 
            return key;
        
        lock (Lock)
        {
            key = _key;
            if (key != null) 
                return key;

            var pemKey = configuration["Jwt:PrivateKeyPem"];
            
            if (string.IsNullOrWhiteSpace(pemKey))
            {
                var keysDir = Path.Combine(AppContext.BaseDirectory, "keys");
                var keyPath = Path.Combine(keysDir, "jwt_private.pem");

                if (File.Exists(keyPath))
                {
                    pemKey = File.ReadAllText(keyPath);
                    Console.WriteLine($"[RsaKeyProvider] Loaded JWT private key from {keyPath}");
                }
                else
                {
                    if (!Directory.Exists(keysDir))
                        Directory.CreateDirectory(keysDir);

                    using var rsaGen = RSA.Create(2048);
                    pemKey = rsaGen.ExportRSAPrivateKeyPem();
                    
                    File.WriteAllText(keyPath, pemKey);
                    Console.WriteLine($"[RsaKeyProvider] Generated new JWT key at {keyPath}");
                }
            }

            try
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(pemKey);

                if (rsa.KeySize < 2048)
                    throw new InvalidOperationException(
                        $"CRITICAL: Insufficient RSA Key Size ({rsa.KeySize} bits). " +
                        "Private Key must be at least 2048 bits long.");
                
                _key = new RsaSecurityKey(rsa) { KeyId = "v1" };
                return _key;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "CRITICAL: Failed to parse JWT private key.");
            }
        }
    }
}
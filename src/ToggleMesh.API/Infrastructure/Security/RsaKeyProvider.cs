using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ToggleMesh.API.Infrastructure.Security;

public static class RsaKeyProvider
{
    private static volatile RsaSecurityKey? _key;
    private static readonly Lock Lock = new();

    public static RsaSecurityKey GetKey(IConfiguration configuration)
    {
        if (_key != null) 
            return _key;
        
        lock (Lock)
        {
            if (_key != null) 
                return _key;

            var pemKey = configuration["Jwt:PrivateKeyPem"];
            if (string.IsNullOrWhiteSpace(pemKey))
                throw new InvalidOperationException(
                    "CRITICAL: Jwt__PrivateKeyPem is missing.");

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
                    "CRITICAL: Failed to parse JWT__PrivateKeyPem.");
            }
        }
    }
}
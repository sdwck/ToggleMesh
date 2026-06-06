using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ToggleMesh.API.Infrastructure.Security;

public static class RsaKeyProvider
{
    private static RsaSecurityKey? _key;

    public static RsaSecurityKey GetKey()
    {
        if (_key != null) 
            return _key;

        var keyPath = Path.Combine(AppContext.BaseDirectory, ".rsa_key");
        var rsa = RSA.Create();
        
        if (File.Exists(keyPath))
            rsa.ImportRSAPrivateKey(File.ReadAllBytes(keyPath), out _);
        else
            File.WriteAllBytes(keyPath, rsa.ExportRSAPrivateKey());

        _key = new RsaSecurityKey(rsa) { KeyId = "v1" };
        return _key;
    }
}
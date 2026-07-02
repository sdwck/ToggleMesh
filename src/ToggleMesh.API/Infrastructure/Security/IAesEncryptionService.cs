namespace ToggleMesh.API.Infrastructure.Security;

public interface IAesEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

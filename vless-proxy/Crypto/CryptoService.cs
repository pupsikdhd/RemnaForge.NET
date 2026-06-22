using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace vless_proxy.Crypto;

public class CryptoService
{
    private const int DegreeOfParallelism = 4;
    private const int MemorySize = 65536;
    private const int Iterations = 4;
    private readonly byte[] _encryptionKey;

    public CryptoService(IConfiguration configuration)
    {
        var keyString = configuration["TOTPEncryptionKey"] ?? "vless-proxy-default-secure-totp-encryption-key-32chars";
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        return AES_Module.Encrypt(plainText, _encryptionKey);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        return AES_Module.Decrypt(cipherText, _encryptionKey);
    }

    public string GetCode(int length = 8)
    {
        string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(allowedChars, length).ToUpper();
    }

    public string GetSHA256(string text)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(text);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes); 
    }

    private static byte[] GenerateSalt()
    {
        byte[] buffer = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
        return buffer;
    }
    
    public string GetArgon2id(string password)
    {
        byte[] salt = GenerateSalt();
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        using (var argon2 = new Argon2id(passwordBytes))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            byte[] hashBytes = argon2.GetBytes(32);
            
            return $"{Convert.ToHexString(salt)}${Convert.ToHexString(hashBytes)}";
        }
    }
    
    public bool VerifyArgon2id(string password, string storedHashWithSalt)
    {
        var parts = storedHashWithSalt.Split('$');
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromHexString(parts[0]);
        byte[] originalHash = Convert.FromHexString(parts[1]);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        using (var argon2 = new Argon2id(passwordBytes))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            byte[] computedHash = argon2.GetBytes(32);
            
            return CryptographicOperations.FixedTimeEquals(computedHash, originalHash);
        }
    }
    
    

}
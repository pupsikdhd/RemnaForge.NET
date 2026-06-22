
using System.Security.Cryptography;
using System.Text;

namespace vless_proxy.Crypto;

public static class AES_Module
{
    public static string Encrypt(string plainText, byte[] key)
    {
        if (string.IsNullOrEmpty(plainText)) throw new ArgumentNullException(nameof(plainText));
        if (key == null || key.Length != 32) throw new ArgumentException("Key must be 32 bytes.");

        using Aes aes = Aes.Create();
        aes.Key = key;
        
        aes.GenerateIV();
        byte[] iv = aes.IV;

        using MemoryStream ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length);

        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (StreamWriter sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }
        
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherText, byte[] key)
    {
        if (string.IsNullOrEmpty(cipherText)) throw new ArgumentNullException(nameof(cipherText));
        if (key == null || key.Length != 32) throw new ArgumentException("Key must be 32 bytes.");

        byte[] allBytes = Convert.FromBase64String(cipherText);
        if (allBytes.Length < 16) throw new InvalidOperationException("Invalid ciphertext data.");
        
        byte[] iv = new byte[16];
        Buffer.BlockCopy(allBytes, 0, iv, 0, 16);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        
        using MemoryStream ms = new MemoryStream(allBytes, 16, allBytes.Length - 16);
        using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using StreamReader sr = new StreamReader(cs, Encoding.UTF8);

        return sr.ReadToEnd();
    }
}
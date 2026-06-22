using OtpNet;

namespace vless_proxy.Crypto;

public class OTP_Module
{
    public static string GenerateSecret()
    {
        byte[] secretKey = KeyGeneration.GenerateRandomKey(20);
        string base32Secret = Base32Encoding.ToString(secretKey);
        return base32Secret;
    }

    public static bool Validate(string code, string secret)
    {
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(
                code, 
                out _, 
                new VerificationWindow(previous: 1, future: 1)
            );
        }
        catch
        {
            return false;
        }
    }
}
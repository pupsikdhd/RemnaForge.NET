using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.TOTP;

public class TOTPVerifyRequest
{
    [Required]
    public string TotpToken { get; set; } = string.Empty;
    [Required]
    public string Code { get; set; } = string.Empty;
}

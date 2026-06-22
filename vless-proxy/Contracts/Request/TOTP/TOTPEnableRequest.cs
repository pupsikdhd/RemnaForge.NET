using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.TOTP;

public class TOTPEnableRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}

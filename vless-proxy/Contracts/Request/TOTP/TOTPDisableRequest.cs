using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.TOTP;

public class TOTPDisableRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}

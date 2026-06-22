using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.ApiKey;

public class CreateApiKeyRequest
{
    [Required]
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
}

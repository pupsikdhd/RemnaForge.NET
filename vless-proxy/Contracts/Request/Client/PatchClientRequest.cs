using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.Client;

public class PatchClientRequest
{
    [Required]
    public Guid Id { get; set; }
    public string? Name { get; set; } = string.Empty;
    public DateTime? ExpireAt { get; set; } = DateTime.MaxValue;
    public int? DeviceLimit { get; set; }
}
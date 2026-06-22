using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Contracts.Request.Client;

public class CreateClientRequest
{
    public string Name { get; set; } = string.Empty;
    [Required]
    public Guid SubscriptionId { get; set; }
    public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddYears(100);
    public int DeviceLimit { get; set; } = 5;
}
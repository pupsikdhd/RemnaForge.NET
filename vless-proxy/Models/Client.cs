using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace vless_proxy.Models;

public class Client
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddYears(100);
    public int DeviceLimit { get; set; } = 5;
    
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;
    
    
}
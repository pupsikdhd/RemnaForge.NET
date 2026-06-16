namespace vless_proxy.Contracts.Response;

public class ClientResponse
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    public Guid SubscriptionId { get; set; } 
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpireAt { get; set; } = DateTime.MaxValue;
    public int DeviceLimit { get; set; } = 5;
}
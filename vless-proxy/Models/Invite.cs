namespace vless_proxy.Models;

public class Invite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddDays(7);
    
}
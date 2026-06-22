namespace vless_proxy.Models;

public class TOTPSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
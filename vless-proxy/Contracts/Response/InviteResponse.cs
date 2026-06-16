namespace vless_proxy.Contracts.Response;

public class InviteResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpireAt { get; set; }
}

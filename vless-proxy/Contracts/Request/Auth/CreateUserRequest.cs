namespace vless_proxy.Contracts.Request.Auth;

public class CreateUserRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public Guid? InviteId { get; set; }
}
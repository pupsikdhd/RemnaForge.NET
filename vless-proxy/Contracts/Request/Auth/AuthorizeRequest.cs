namespace vless_proxy.Contracts.Request.Auth;

public class AuthorizeRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
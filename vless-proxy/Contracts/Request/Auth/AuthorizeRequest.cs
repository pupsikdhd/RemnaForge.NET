namespace vless_proxy.Contracts.Contracts.Request;

public class AuthorizeRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
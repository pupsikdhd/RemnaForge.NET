namespace vless_proxy.Contracts.Response.Auth;

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool RequiresTotp { get; set; }
    public string? TotpToken { get; set; }
}

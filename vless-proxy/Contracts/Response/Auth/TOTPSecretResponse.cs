namespace vless_proxy.Contracts.Response.Auth;

public class TOTPSecretResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
}

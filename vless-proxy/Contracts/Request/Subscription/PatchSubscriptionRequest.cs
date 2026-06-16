namespace vless_proxy.Contracts.Request.Subscription;

public class PatchSubscriptionRequest
{
    public Guid SubscriptionId { get; set; }
    public string? Name { get; set; }
    public string URL { get; set; } = string.Empty;
    public string? HWID { get; set; }
    public string? X_DEVICE_OS { get; set; }
    public string? X_VER_OS { get; set; }
    public string? USER_AGENT { get; set; }
    public string? X_APP_VERSION { get; set; }
    
    public string X_Device_Model {get; set;} = string.Empty;
    public string? ACCEPT_ENCODING { get; set; }
}
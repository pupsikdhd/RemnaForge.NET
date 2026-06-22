namespace vless_proxy.Contracts.Response.Subscription;

public class SubscriptionResponse
{
    public Guid SubscriptionId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public string HWID { get; set; } = string.Empty;
    public string X_DEVICE_OS {get; set;} = string.Empty;
    public string X_VER_OS {get; set;} = string.Empty;
    public string USER_AGENT {get; set;} = string.Empty;
    public string X_APP_VERSION {get; set;} = string.Empty;
    public string X_Device_Model {get; set;} = string.Empty;
    public string ACCEPT_ENCODING {get; set;} = "gzip";
}

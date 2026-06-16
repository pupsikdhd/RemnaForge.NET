using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Models;

public class Subscription
{
    [Key]
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
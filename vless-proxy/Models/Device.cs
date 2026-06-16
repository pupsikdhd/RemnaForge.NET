namespace vless_proxy.Models;

public class Device
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    public string HWID { get; set; } = string.Empty;
    public string USER_AGENT {get; set;} = string.Empty;
    
    public Guid ClientId { get; set; } = Guid.Empty;
}
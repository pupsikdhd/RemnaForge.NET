namespace vless_proxy.Contracts.Response.Device;

public class DeviceResponse
{
    public Guid Id { get; set; }
    public string HWID { get; set; } = string.Empty;
    public string USER_AGENT { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
}

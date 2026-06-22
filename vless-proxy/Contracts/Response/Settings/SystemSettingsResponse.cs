namespace vless_proxy.Contracts.Response.Settings;

public class SystemSettingsResponse
{
    public bool AllowRegistration { get; set; }
    public bool UseCaching { get; set; }
    public int CacheDurationInMinutes { get; set; }
}

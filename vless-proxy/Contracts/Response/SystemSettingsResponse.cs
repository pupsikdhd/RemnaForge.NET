namespace vless_proxy.Contracts.Response;

public class SystemSettingsResponse
{
    public bool AllowRegistration { get; set; }
    public bool UseCaching { get; set; }
    public int CacheDurationInMinutes { get; set; }
}
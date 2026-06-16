namespace vless_proxy.Contracts.Request.Settings;

public class PatchSystemSettingsRequest
{
    public bool? AllowRegistration { get; set; }
    public bool? UseCaching { get; set; }
    public int? CacheDurationInMinutes { get; set; }
}
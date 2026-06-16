namespace vless_proxy.Models;

public class SystemSettings
{
    public int Id { get; set; }

    public bool AllowRegistration { get; set; } = true;
    public bool UseCaching { get; set; } = false;
    public TimeSpan CacheDurationInMinutes { get; set; } =
        TimeSpan.FromMinutes(30);
}
using System;
using System.Collections.Generic;

namespace vless_proxy.Contracts.Response.Client;

public class PublicClientDevicesResponse
{
    public string ClientName { get; set; } = string.Empty;
    public int DeviceLimit { get; set; }
    public List<PublicDeviceResponse> Devices { get; set; } = new();
}

public class PublicDeviceResponse
{
    public Guid Id { get; set; }
    public string Hwid { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

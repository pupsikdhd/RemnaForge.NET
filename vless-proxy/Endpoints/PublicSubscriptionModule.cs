using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using vless_proxy.Contracts.Response.Client;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class PublicSubscriptionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/sub/{slug}", HandleSubscriptionRequest)
           .RequireRateLimiting("public-sub-limiter");
        
        var publicGroup = app.MapGroup("/api/public/clients/{slug}")
           .RequireRateLimiting("public-sub-limiter");
           
        publicGroup.MapGet("/devices", GetPublicClientDevicesHandler);
        publicGroup.MapDelete("/devices/{deviceId}", DeletePublicClientDeviceHandler);
    }

    private static async Task<IResult> HandleSubscriptionRequest(
        string slug, HttpContext httpContext, DBContext dbContext, 
        IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        var client = await dbContext.Clients.FirstOrDefaultAsync(c => c.Slug == slug);
        
        if (client == null) return Results.NotFound("Subscription not found.");

        if (DateTime.UtcNow > client.ExpireAt)
            return Results.Text("Your subscription has expired. Please contact administration.", statusCode: 403);

        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        if (acceptHeader.Contains("text/html") || userAgent.Contains("Mozilla"))
            return Results.Redirect($"/client/{slug}");

        string hwidHeader = httpContext.Request.Headers["X-HWID"].ToString();
        
        string hwid = !string.IsNullOrEmpty(hwidHeader) ? hwidHeader : httpContext.Request.Headers["X-Device-ID"].ToString();

        if (string.IsNullOrEmpty(hwid))
            return Results.BadRequest("Missing X-HWID header. Please use a supported vless client.");

        var deviceResult = await CheckAndRegisterDeviceAsync(dbContext, client, hwid, userAgent);
        
        if (deviceResult != null) return deviceResult;

        var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Id == 1);
        
        memoryCache.Set($"last_sub_update_{client.Id}", DateTime.UtcNow);
        bool useCache = settings?.UseCaching ?? false;
        
        var subscriptionData = await GetSubscriptionDataAsync(dbContext, memoryCache, httpClientFactory, client.SubscriptionId, useCache, settings?.CacheDurationInMinutes);
        if (subscriptionData == null)
            return Results.NotFound("Upstream subscription server configuration error.");

        return await SendCompressedResponseAsync(httpContext, subscriptionData);
    }

    private static async Task<IResult?> CheckAndRegisterDeviceAsync(DBContext dbContext, Client client, string hwid, string userAgent)
    {
        if (await dbContext.Devices.AnyAsync(d => d.ClientId == client.Id && d.HWID == hwid))
            return null;

        if (client.DeviceLimit > 0 && await dbContext.Devices.CountAsync(d => d.ClientId == client.Id) >= client.DeviceLimit)
            return Results.Text("Device limit exceeded for this subscription.", statusCode: 429);

        dbContext.Devices.Add(new Device { Id = Guid.NewGuid(), ClientId = client.Id, HWID = hwid, USER_AGENT = userAgent });
        await dbContext.SaveChangesAsync();
        
        return null;
    }

    private static async Task<byte[]?> GetSubscriptionDataAsync(DBContext dbContext, IMemoryCache memoryCache, 
        IHttpClientFactory httpClientFactory, Guid subscriptionId, bool useCache, TimeSpan? cacheDuration)
    {
        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        if (subscription == null || string.IsNullOrEmpty(subscription.URL)) return null;

        if (!useCache) return await DownloadSubscriptionAsync(subscription, httpClientFactory);

        string cacheKey = $"sub_data_{subscription.SubscriptionId}";
        if (memoryCache.TryGetValue(cacheKey, out byte[]? cachedData) && cachedData != null)
            return cachedData;

        var data = await DownloadSubscriptionAsync(subscription, httpClientFactory);
        memoryCache.Set(cacheKey, data, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheDuration ?? TimeSpan.FromMinutes(30) });
        return data;
    }

    private static async Task<IResult> SendCompressedResponseAsync(HttpContext httpContext, byte[] data)
    {
        var acceptEncoding = httpContext.Request.Headers.AcceptEncoding.ToString();
        var encoding = acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase) ? "br" :
                       acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase) ? "gzip" :
                       acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase) ? "deflate" : null;

        if (encoding != null)
        {
            using (var ms = new MemoryStream())
            {
                await using (Stream compressStream = encoding switch
                {
                    "br" => new BrotliStream(ms, CompressionLevel.Fastest),
                    "gzip" => new GZipStream(ms, CompressionLevel.Fastest),
                    "deflate" => new DeflateStream(ms, CompressionLevel.Fastest),
                    _ => throw new InvalidOperationException()
                })
                {
                    await compressStream.WriteAsync(data);
                }
                data = ms.ToArray();
            }
            httpContext.Response.Headers.ContentEncoding = encoding;
        }

        return Results.File(data, "text/plain; charset=utf-8");
    }

    private static async Task<byte[]> DownloadSubscriptionAsync(Subscription sub, IHttpClientFactory httpClientFactory)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, sub.URL);
        
        Action<string, string?> addHeader = (name, value) => { if (!string.IsNullOrEmpty(value)) request.Headers.TryAddWithoutValidation(name, value); };
        
        if (!string.IsNullOrEmpty(sub.USER_AGENT)) request.Headers.UserAgent.ParseAdd(sub.USER_AGENT);
        if (!string.IsNullOrEmpty(sub.ACCEPT_ENCODING)) request.Headers.AcceptEncoding.ParseAdd(sub.ACCEPT_ENCODING);
        
        addHeader("X-HWID", sub.HWID);
        addHeader("X-Device-OS", sub.X_DEVICE_OS);
        addHeader("X-Ver-OS", sub.X_VER_OS);
        addHeader("X-App-Version", sub.X_APP_VERSION);
        addHeader("X-Device-Model", sub.X_Device_Model);

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault()?.ToLowerInvariant();

        return encoding switch
        {
            "br" => Decompress(bytes, s => new BrotliStream(s, CompressionMode.Decompress)),
            "gzip" => Decompress(bytes, s => new GZipStream(s, CompressionMode.Decompress)),
            "deflate" => Decompress(bytes, s => new DeflateStream(s, CompressionMode.Decompress)),
            _ => bytes
        };
    }

    private static byte[] Decompress(byte[] data, Func<Stream, Stream> createDecompressor)
    {
        using var ms = new MemoryStream(data);
        using var decompressor = createDecompressor(ms);
        using var outMs = new MemoryStream();
        decompressor.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static async Task<IResult> GetPublicClientDevicesHandler(string slug, DBContext dbContext)
    {
        var client = await dbContext.Clients.FirstOrDefaultAsync(c => c.Slug == slug);
        if (client == null) return Results.NotFound(new { success = false, message = "Client subscription not found." });
        if (DateTime.UtcNow > client.ExpireAt) return Results.Json(new { success = false, message = "Your subscription has expired." }, statusCode: 403);

        var devices = await dbContext.Devices.AsNoTracking().Where(d => d.ClientId == client.Id)
            .Select(d => new PublicDeviceResponse
            {
                Id = d.Id,
                Hwid = d.HWID,
                UserAgent = d.USER_AGENT
            }).ToListAsync();

        return Results.Ok(new PublicClientDevicesResponse
        {
            ClientName = client.Name,
            DeviceLimit = client.DeviceLimit,
            Devices = devices
        });
    }

    private static async Task<IResult> DeletePublicClientDeviceHandler(string slug, Guid deviceId, DBContext dbContext)
    {
        var client = await dbContext.Clients.FirstOrDefaultAsync(c => c.Slug == slug);
        if (client == null) return Results.NotFound(new { success = false, message = "Client subscription not found." });
        if (DateTime.UtcNow > client.ExpireAt) return Results.Json(new { success = false, message = "Your subscription has expired." }, statusCode: 403);

        var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.ClientId == client.Id);
        if (device == null) return Results.NotFound(new { success = false, message = "Device not found." });

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }
}
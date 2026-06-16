
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class PublicSubscriptionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Публичный эндпоинт для ссылки подписки. Сюда переходят и люди, и vless-клиенты.
        app.MapGet("/sub/{slug}", HandleSubscriptionRequest);
        
        var publicGroup = app.MapGroup("/api/public/clients/{slug}");
        publicGroup.MapGet("/devices", GetPublicClientDevicesHandler);
        publicGroup.MapDelete("/devices/{deviceId}", DeletePublicClientDeviceHandler);
    }

    private static async Task<IResult> HandleSubscriptionRequest(
        string slug,
        HttpContext httpContext,
        DBContext dbContext,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache)
    {
        // 1. Ищем клиента по его уникальному Slug
        var client = await dbContext.Clients
            .FirstOrDefaultAsync(c => c.Slug == slug);

        if (client == null)
            return Results.NotFound("Subscription not found.");

        // 2. Проверяем, не истек ли срок действия клиента
        if (DateTime.UtcNow > client.ExpireAt)
            return Results.Text("Your subscription has expired. Please contact administration.", statusCode: 403);

        // 3. ОПРЕДЕЛЯЕМ ТИП ЗАПРОСА: Браузер или Vless-клиент?
        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        // Если запрос пришел от браузера (он просит HTML), отдаем интерфейс
        if (acceptHeader.Contains("text/html") || userAgent.Contains("Mozilla"))
        {
            // Здесь мы перенаправляем пользователя на страницу React-приложения, 
            // передавая slug в качестве параметра (или роута фронтенда),
            // чтобы фронт сам сделал красивый QR-код и показал инфу.
            return Results.Redirect($"/client/{slug}"); 
        }

        // =================================================================
        // ЛОГИКА ДЛЯ VLESS-КЛИЕНТА
        // =================================================================

        // Извлекаем HWID из заголовков (vless-клиенты обычно шлют его в X-HWID или кастомных заголовках)
        // Если заголовка нет, можно использовать IP-адрес как крайний случай, но лучше требовать HWID
        string hwid = httpContext.Request.Headers["X-HWID"].ToString();
        if (string.IsNullOrEmpty(hwid))
        {
            hwid = httpContext.Request.Headers["X-Device-ID"].ToString(); // Альтернативный заголовок
        }

        if (string.IsNullOrEmpty(hwid))
            return Results.BadRequest("Missing X-HWID header. Please use a supported vless client.");

        // КОНТРОЛЬ УСТРОЙСТВ
        // Проверяем, зарегистрировано ли уже это устройство
        var deviceExists = await dbContext.Devices
            .AnyAsync(d => d.ClientId == client.Id && d.HWID == hwid);

        if (!deviceExists)
        {
            // Если устройство новое, проверяем лимиты (0 = безлимитно)
            if (client.DeviceLimit > 0)
            {
                var currentDevicesCount = await dbContext.Devices.CountAsync(d => d.ClientId == client.Id);
                if (currentDevicesCount >= client.DeviceLimit)
                {
                    return Results.Text("Device limit exceeded for this subscription.", statusCode: 429);
                }
            }

            // Вписываемся в лимит или безлимит -> добавляем новое устройство в базу
            var newDevice = new Device
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                HWID = hwid,
                USER_AGENT = userAgent
            };
            dbContext.Devices.Add(newDevice);
            await dbContext.SaveChangesAsync();
        }

        // ПОЛУЧЕНИЕ ОРИГИНАЛЬНОЙ ПОДПИСКИ (С КЭШЕМ ИЛИ БЕЗ)
        // Получаем настройки, чтобы узнать, включен ли кэш
        var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Id == 1);
        bool useCache = settings?.UseCaching ?? false;

        // Получаем саму сущность подписки
        var subscription = await dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == client.SubscriptionId);

        if (subscription == null || string.IsNullOrEmpty(subscription.URL))
            return Results.NotFound("Upstream subscription server configuration error.");

        byte[] subscriptionData;

        if (useCache)
        {
            string cacheKey = $"sub_data_{subscription.SubscriptionId}";
            
            // Пытаемся достать данные из памяти
            if (!memoryCache.TryGetValue(cacheKey, out subscriptionData!))
            {
                // Если в кэше нет — качаем из интернета
                subscriptionData = await DownloadSubscriptionAsync(subscription, httpClientFactory);
                
                // Сохраняем в кэш на указанное в настройках время
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = settings?.CacheDurationInMinutes ?? TimeSpan.FromMinutes(30)
                };
                memoryCache.Set(cacheKey, subscriptionData, cacheOptions);
            }
        }
        else
        {
            // Кэш выключен — качаем напрямую
            subscriptionData = await DownloadSubscriptionAsync(subscription, httpClientFactory);
        }

        // ОТПРАВЛЯЕМ ОТВЕТ КЛИЕНТУ (Аналог твоего Python кода)
        // Сжимаем ответ, если клиент поддерживает сжатие
        var clientAcceptEncoding = httpContext.Request.Headers.AcceptEncoding.ToString();
        if (clientAcceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var bs = new BrotliStream(ms, CompressionLevel.Fastest))
            {
                await bs.WriteAsync(subscriptionData, 0, subscriptionData.Length);
            }
            subscriptionData = ms.ToArray();
            httpContext.Response.Headers.ContentEncoding = "br";
        }
        else if (clientAcceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var gs = new GZipStream(ms, CompressionLevel.Fastest))
            {
                await gs.WriteAsync(subscriptionData, 0, subscriptionData.Length);
            }
            subscriptionData = ms.ToArray();
            httpContext.Response.Headers.ContentEncoding = "gzip";
        }
        else if (clientAcceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var ds = new DeflateStream(ms, CompressionLevel.Fastest))
            {
                await ds.WriteAsync(subscriptionData, 0, subscriptionData.Length);
            }
            subscriptionData = ms.ToArray();
            httpContext.Response.Headers.ContentEncoding = "deflate";
        }

        // Возвращаем бинарные данные с правильным типом содержимого
        return Results.File(
            fileContents: subscriptionData, 
            contentType: "text/plain; charset=utf-8" // Большинство клиентов ждут text/plain или base64
        );
    }

    // Вспомогательный метод для загрузки данных с оригинального сервера
    private static async Task<byte[]> DownloadSubscriptionAsync(Subscription sub, IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient();

        // Формируем запрос
        var request = new HttpRequestMessage(HttpMethod.Get, sub.URL);

        // Мапим заголовки из сущности Subscription (как в твоем Python скрипте)
        if (!string.IsNullOrEmpty(sub.USER_AGENT))
            request.Headers.UserAgent.ParseAdd(sub.USER_AGENT);
            
        if (!string.IsNullOrEmpty(sub.HWID))
            request.Headers.Add("X-HWID", sub.HWID);
            
        if (!string.IsNullOrEmpty(sub.X_DEVICE_OS))
            request.Headers.Add("X-Device-OS", sub.X_DEVICE_OS);
            
        if (!string.IsNullOrEmpty(sub.X_VER_OS))
            request.Headers.Add("X-Ver-OS", sub.X_VER_OS);
            
        if (!string.IsNullOrEmpty(sub.X_APP_VERSION))
            request.Headers.Add("X-App-Version", sub.X_APP_VERSION);
        
        if (!string.IsNullOrEmpty(sub.X_Device_Model))
            request.Headers.Add("X-Device-Model", sub.X_Device_Model);
            
        if (!string.IsNullOrEmpty(sub.ACCEPT_ENCODING))
            request.Headers.AcceptEncoding.ParseAdd(sub.ACCEPT_ENCODING);

        // Выполняем запрос к реальному серверу подписок
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var contentBytes = await response.Content.ReadAsByteArrayAsync();

        // Если upstream вернул сжатые данные (и HttpClient их не распаковал автоматически),
        // распаковываем их вручную, чтобы кэшировать в распакованном виде.
        var contentEncodings = response.Content.Headers.ContentEncoding.Select(e => e.ToLowerInvariant()).ToList();
        if (contentEncodings.Contains("br"))
        {
            using var ms = new MemoryStream(contentBytes);
            using var bs = new BrotliStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            await bs.CopyToAsync(outMs);
            contentBytes = outMs.ToArray();
        }
        else if (contentEncodings.Contains("gzip"))
        {
            using var ms = new MemoryStream(contentBytes);
            using var gs = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            await gs.CopyToAsync(outMs);
            contentBytes = outMs.ToArray();
        }
        else if (contentEncodings.Contains("deflate"))
        {
            using var ms = new MemoryStream(contentBytes);
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            await ds.CopyToAsync(outMs);
            contentBytes = outMs.ToArray();
        }

        return contentBytes;
    }

    private static async Task<IResult> GetPublicClientDevicesHandler(string slug, DBContext dbContext)
    {
        var client = await dbContext.Clients.FirstOrDefaultAsync(c => c.Slug == slug);
        if (client == null)
            return Results.NotFound(new { success = false, message = "Client subscription not found." });

        if (DateTime.UtcNow > client.ExpireAt)
            return Results.Json(new { success = false, message = "Your subscription has expired. Please contact administration." }, statusCode: 403);

        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(d => d.ClientId == client.Id)
            .Select(d => new 
            {
                id = d.Id,
                hwid = d.HWID,
                userAgent = d.USER_AGENT
            })
            .ToListAsync();

        return Results.Ok(new
        {
            clientName = client.Name,
            deviceLimit = client.DeviceLimit,
            devices = devices
        });
    }

    private static async Task<IResult> DeletePublicClientDeviceHandler(string slug, Guid deviceId, DBContext dbContext)
    {
        var client = await dbContext.Clients.FirstOrDefaultAsync(c => c.Slug == slug);
        if (client == null)
            return Results.NotFound(new { success = false, message = "Client subscription not found." });

        if (DateTime.UtcNow > client.ExpireAt)
            return Results.Json(new { success = false, message = "Your subscription has expired." }, statusCode: 403);

        var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.ClientId == client.Id);
        if (device == null)
            return Results.NotFound(new { success = false, message = "Device not found." });

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }
}
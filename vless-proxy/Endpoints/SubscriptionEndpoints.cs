using Microsoft.EntityFrameworkCore;
using vless_proxy.Contracts.Request.Subscription;
using vless_proxy.Contracts.Response.Subscription;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class SubscriptionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscriptions").AddEndpointFilter<AdminAuthFilter>();
        
        group.MapGet("/", GetSubscriptionsHandler);
        group.MapPost("/", CreateSubscriptionHandler);
        group.MapPatch("/", PatchSubscriptionHandler);
        group.MapDelete("/{id}", DeleteSubscriptionHandler);
    }

    private static async Task<IResult> CreateSubscriptionHandler(CreateSubscriptionRequest request, DBContext context)
    {
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            URL = request.URL,
            Name = request.Name,
            HWID = request.HWID,
            X_DEVICE_OS = request.X_DEVICE_OS,
            X_VER_OS = request.X_VER_OS,
            USER_AGENT = request.USER_AGENT,
            X_APP_VERSION = request.X_APP_VERSION,
            X_Device_Model = request.X_Device_Model,
            ACCEPT_ENCODING = string.IsNullOrEmpty(request.ACCEPT_ENCODING) ? "gzip" : request.ACCEPT_ENCODING
        };
        
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();
        
        var dto = new SubscriptionResponse
        {
            SubscriptionId = subscription.SubscriptionId,
            URL =  subscription.URL,
            Name = subscription.Name,
            HWID = subscription.HWID,
            X_DEVICE_OS = subscription.X_DEVICE_OS,
            X_VER_OS = subscription.X_VER_OS,
            USER_AGENT = subscription.USER_AGENT,
            X_APP_VERSION = subscription.X_APP_VERSION,
            X_Device_Model = subscription.X_Device_Model,
            ACCEPT_ENCODING = subscription.ACCEPT_ENCODING
        };
        
        return Results.Ok(dto);
    }

    private static async Task<IResult> PatchSubscriptionHandler(PatchSubscriptionRequest request, DBContext context)
    {
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == request.SubscriptionId);
        if (subscription == null)
            return Results.NotFound(new { success = false, message = "Subscription not found." });
        
        if (!string.IsNullOrEmpty(request.Name)) 
            subscription.Name = request.Name;
            
        if(!string.IsNullOrEmpty(request.URL))
            subscription.URL = request.URL;
        
        if (!string.IsNullOrEmpty(request.HWID)) 
            subscription.HWID = request.HWID;
            
        if (!string.IsNullOrEmpty(request.X_DEVICE_OS)) 
            subscription.X_DEVICE_OS = request.X_DEVICE_OS;
            
        if (!string.IsNullOrEmpty(request.X_VER_OS)) 
            subscription.X_VER_OS = request.X_VER_OS;
            
        if (!string.IsNullOrEmpty(request.USER_AGENT)) 
            subscription.USER_AGENT = request.USER_AGENT;
            
        if (!string.IsNullOrEmpty(request.X_APP_VERSION)) 
            subscription.X_APP_VERSION = request.X_APP_VERSION;
            
        if (!string.IsNullOrEmpty(request.X_Device_Model)) 
            subscription.X_Device_Model = request.X_Device_Model;
        
        if (!string.IsNullOrEmpty(request.ACCEPT_ENCODING)) 
            subscription.ACCEPT_ENCODING = request.ACCEPT_ENCODING;

        await context.SaveChangesAsync();

        var dto = new SubscriptionResponse
        {
            SubscriptionId = subscription.SubscriptionId,
            URL = subscription.URL,
            Name = subscription.Name,
            HWID = subscription.HWID,
            X_DEVICE_OS = subscription.X_DEVICE_OS,
            X_VER_OS = subscription.X_VER_OS,
            USER_AGENT = subscription.USER_AGENT,
            X_APP_VERSION = subscription.X_APP_VERSION,
            X_Device_Model = subscription.X_Device_Model,
            ACCEPT_ENCODING = subscription.ACCEPT_ENCODING
        };

        return Results.Ok(dto);
    }
    
    private static async Task<IResult> DeleteSubscriptionHandler(Guid id, DBContext context)
    {
        var deletedCount = await context.Subscriptions
            .Where(s => s.SubscriptionId == id)
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound(new { success = false, message = "Subscription not found." });
            
        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> GetSubscriptionsHandler(DBContext context)
    {
        // Оптимизировано: проекция Select транслируется прямо в SQL, 
        // а AsNoTracking отключает отслеживание изменений для экономии памяти
        var subs = await context.Subscriptions
            .AsNoTracking()
            .Select(s => new SubscriptionResponse
            {
                SubscriptionId = s.SubscriptionId,
                Name = s.Name,
                URL = s.URL,
                X_DEVICE_OS = s.X_DEVICE_OS,
                HWID = s.HWID,
                X_VER_OS = s.X_VER_OS,
                USER_AGENT = s.USER_AGENT,
                X_APP_VERSION = s.X_APP_VERSION,
                X_Device_Model = s.X_Device_Model,
                ACCEPT_ENCODING = s.ACCEPT_ENCODING
            })
            .ToListAsync();
            
        return Results.Ok(subs);
    }
}
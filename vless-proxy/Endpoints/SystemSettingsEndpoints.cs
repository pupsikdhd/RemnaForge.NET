using Microsoft.EntityFrameworkCore;
using vless_proxy.Contracts.Request.Settings;
using vless_proxy.Contracts.Response;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class SystemSettingsEndpoints : ICarterModule
{
    private const int SettingsId = 1;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").AddEndpointFilter<SessionAuthFilter>();

        group.MapGet("/", GetSettingsHandler);
        group.MapPatch("/", PatchSettingsHandler);
    }

    private static async Task<IResult> GetSettingsHandler(DBContext context)
    {
        var settings = await context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SettingsId);

        if (settings == null)
            return Results.NotFound(new { success = false, message = "System settings not initialized." });

        var dto = new SystemSettingsResponse
        {
            AllowRegistration = settings.AllowRegistration,
            UseCaching = settings.UseCaching,
            CacheDurationInMinutes = (int)settings.CacheDurationInMinutes.TotalMinutes
        };

        return Results.Ok(dto);
    }

    private static async Task<IResult> PatchSettingsHandler(PatchSystemSettingsRequest request, DBContext context)
    {
        var settings = await context.SystemSettings.FirstOrDefaultAsync(s => s.Id == SettingsId);
        if (settings == null)
            return Results.NotFound(new { success = false, message = "System settings not found." });
        
        if (request.AllowRegistration.HasValue)
        {
            settings.AllowRegistration = request.AllowRegistration.Value;
        }
        
        if (request.UseCaching.HasValue)
        {
            settings.UseCaching = request.UseCaching.Value;
        }
        
        if (settings.UseCaching)
        {
            if (request.CacheDurationInMinutes.HasValue && request.CacheDurationInMinutes.Value > 0)
            {
                settings.CacheDurationInMinutes = TimeSpan.FromMinutes(request.CacheDurationInMinutes.Value);
            }
        }
        else
        {
            settings.CacheDurationInMinutes = TimeSpan.Zero; 
        }

        await context.SaveChangesAsync();
        
        var dto = new SystemSettingsResponse
        {
            AllowRegistration = settings.AllowRegistration,
            UseCaching = settings.UseCaching,
            CacheDurationInMinutes = (int)settings.CacheDurationInMinutes.TotalMinutes
        };

        return Results.Ok(dto);
    }
}
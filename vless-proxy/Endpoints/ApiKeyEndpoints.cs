using Microsoft.EntityFrameworkCore;
using vless_proxy.Contracts.Request.ApiKey;
using vless_proxy.Contracts.Response.ApiKey;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class ApiKeyEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/apikeys").AddEndpointFilter<SessionAuthFilter>();

        group.MapGet("/", GetApiKeysHandler);
        group.MapPost("/", CreateApiKeyHandler);
        group.MapPut("/{id:guid}", UpdateApiKeyHandler);
        group.MapDelete("/{id:guid}", DeleteApiKeyHandler);
    }

    private static async Task<IResult> GetApiKeysHandler(DBContext context)
    {
        var keys = await context.ApiKeys
            .AsNoTracking()
            .Select(k => new ApiKeyResponse
            {
                Id = k.Id,
                Key = k.Key,
                Description = k.Description,
                CreatedAt = k.CreatedAt
            })
            .ToListAsync();
        return Results.Ok(keys);
    }

    private static async Task<IResult> CreateApiKeyHandler(CreateApiKeyRequest request, DBContext context)
    {
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Description = request.Description ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        context.ApiKeys.Add(apiKey);
        await context.SaveChangesAsync();

        var response = new ApiKeyResponse
        {
            Id = apiKey.Id,
            Key = apiKey.Key,
            Description = apiKey.Description,
            CreatedAt = apiKey.CreatedAt
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateApiKeyHandler(Guid id, UpdateApiKeyRequest request, DBContext context)
    {
        var apiKey = await context.ApiKeys.FindAsync(id);
        if (apiKey == null)
            return Results.NotFound(new { success = false, message = "API Key not found" });

        if (!string.IsNullOrEmpty(request.Key))
        {
            apiKey.Key = request.Key;
        }
        
        if (request.Description != null)
        {
            apiKey.Description = request.Description;
        }

        await context.SaveChangesAsync();

        var response = new ApiKeyResponse
        {
            Id = apiKey.Id,
            Key = apiKey.Key,
            Description = apiKey.Description,
            CreatedAt = apiKey.CreatedAt
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteApiKeyHandler(Guid id, DBContext context)
    {
        var apiKey = await context.ApiKeys.FindAsync(id);
        if (apiKey == null)
            return Results.NotFound(new { success = false, message = "API Key not found" });

        context.ApiKeys.Remove(apiKey);
        await context.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }
}

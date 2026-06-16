using Microsoft.EntityFrameworkCore;
using vless_proxy.Contracts.Contracts.Request;
using vless_proxy.Contracts.Response;
using vless_proxy.Crypto;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class ClientEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clients").AddEndpointFilter<SessionAuthFilter>();
        
        group.MapGet("/", GetClientsHandler);
        group.MapPost("/", CreateClientHandler);
        group.MapPatch("/", PatchClientHandler);
        group.MapDelete("/{id}", DeleteClientHandler);
        
        group.MapGet("/{clientId}/devices", GetClientDevicesHandler);
        group.MapDelete("/{clientId}/devices/{deviceId}", DeleteClientDeviceHandler);
    }
    
    private static async Task<IResult> DeleteClientHandler(Guid id, DBContext context)
    {
        var client = await context.Clients.FindAsync(id);
        if (client == null)
            return Results.NotFound();
        context.Clients.Remove(client);
        await context.SaveChangesAsync();
        
        return Results.Ok(new {success = true});
    }

    private static async Task<IResult> PatchClientHandler(PatchClientRequest request, DBContext context)
    {
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Id == request.Id);
        if (client == null)
            return Results.NotFound();

        if (request.DeviceLimit.HasValue)
        {
            client.DeviceLimit = request.DeviceLimit.Value;
        }
        
        client.ExpireAt = request.ExpireAt == null ? client.ExpireAt : request.ExpireAt.Value;
        client.Name = request.Name == null ? client.Name : request.Name;
            
        context.Clients.Update(client);
        await context.SaveChangesAsync();

        var dto = new ClientResponse
        {
            Id = client.Id,
            SubscriptionId = client.SubscriptionId,
            Slug = client.Slug,
            Name = client.Name,
            DeviceLimit = client.DeviceLimit,
            CreatedAt = client.CreatedAt,
            ExpireAt = client.ExpireAt
        };
        
        return Results.Ok(dto);
    }
    
    
    private static async Task<IResult> CreateClientHandler(CreateClientRequest request, DBContext context, CryptoService  cryptoService)
    {
        if (request.SubscriptionId == Guid.Empty)
            return Results.BadRequest(new {success = false, message = "SubscriptionId is required"});
        
        var subscription = await context.Subscriptions.FindAsync(request.SubscriptionId);
        if (subscription == null)
            return Results.NotFound(new {success = false, message = "SubscriptionId is not found"});
        
        
        var client = new Client
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.SubscriptionId,
            Slug = cryptoService.GetCode(20),
            Name = request.Name,
            DeviceLimit = request.DeviceLimit,
            ExpireAt = request.ExpireAt,
            CreatedAt = DateTime.UtcNow
        };
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        
        var dto = new ClientResponse
        {
            Id = client.Id,
            SubscriptionId =  client.SubscriptionId,
            Slug = client.Slug,
            Name = client.Name,
            DeviceLimit = client.DeviceLimit,
            CreatedAt = client.CreatedAt,
            ExpireAt = client.ExpireAt
        };
        
        return Results.Ok(dto);
    }

    
    
    
    
    private static async Task<IResult> GetClientsHandler(DBContext context)
    {
        var dtos = await context.Clients
            .AsNoTracking()
            .Select(c => new ClientResponse
            {
                Id = c.Id,
                SubscriptionId =  c.SubscriptionId,
                Slug = c.Slug,
                Name = c.Name,
                DeviceLimit = c.DeviceLimit,
                CreatedAt = c.CreatedAt,
                ExpireAt = c.ExpireAt
            })
            .ToListAsync();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetClientDevicesHandler(Guid clientId, DBContext context)
    {
        var clientExists = await context.Clients.AnyAsync(c => c.Id == clientId);
        if (!clientExists)
            return Results.NotFound(new { success = false, message = "Client not found." });

        var devices = await context.Devices
            .AsNoTracking()
            .Where(d => d.ClientId == clientId)
            .Select(d => new DeviceResponse
            {
                Id = d.Id,
                HWID = d.HWID,
                USER_AGENT = d.USER_AGENT,
                ClientId = d.ClientId
            })
            .ToListAsync();

        return Results.Ok(devices);
    }

    private static async Task<IResult> DeleteClientDeviceHandler(Guid clientId, Guid deviceId, DBContext context)
    {
        var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.ClientId == clientId);
        if (device == null)
            return Results.NotFound(new { success = false, message = "Device not found for this client." });

        context.Devices.Remove(device);
        await context.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }
}
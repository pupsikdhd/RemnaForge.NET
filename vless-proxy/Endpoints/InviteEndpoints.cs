using Microsoft.EntityFrameworkCore;
using vless_proxy.Contracts.Request.Invite;
using vless_proxy.Contracts.Response;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class InviteEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invites").AddEndpointFilter<SessionAuthFilter>();
        
        group.MapGet("/", GetInvitesHandler);
        group.MapPost("/", CreateInviteHandler);
        group.MapDelete("/{id}", DeleteInviteHandler);
    }

    private static async Task<IResult> GetInvitesHandler(DBContext context)
    {
        var invites = await context.Invites
            .AsNoTracking()
            .Select(i => new InviteResponse
            {
                Id = i.Id,
                Description = i.Description,
                CreatedAt = i.CreatedAt,
                ExpireAt = i.ExpireAt
            })
            .ToListAsync();
            
        return Results.Ok(invites);
    }

    private static async Task<IResult> CreateInviteHandler(CreateInviteRequest request, DBContext context)
    {
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            ExpireAt = DateTime.UtcNow.AddDays(request.ExpireDays > 0 ? request.ExpireDays : 7)
        };
        
        context.Invites.Add(invite);
        await context.SaveChangesAsync();
        
        var dto = new InviteResponse
        {
            Id = invite.Id,
            Description = invite.Description,
            CreatedAt = invite.CreatedAt,
            ExpireAt = invite.ExpireAt
        };
        
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteInviteHandler(Guid id, DBContext context)
    {
        var deletedCount = await context.Invites
            .Where(i => i.Id == id)
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound(new { success = false, message = "Invite not found." });
            
        return Results.Ok(new { success = true });
    }
}

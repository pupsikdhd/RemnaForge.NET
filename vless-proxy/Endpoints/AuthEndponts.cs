
using Microsoft.EntityFrameworkCore;
using vless_proxy.Auth;
using vless_proxy.Contracts.Contracts.Request;
using vless_proxy.Crypto;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class AuthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapPost("/login", AuthorizeHandler).RequireRateLimiting("auth-limiter");
        group.MapPost("/create", CreateUserHandler).RequireRateLimiting("auth-limiter");
        
        group.MapPost("/logout", LogoutHandler).AddEndpointFilter<SessionAuthFilter>();
    }

    private static IResult LogoutHandler(HttpContext context, AuthService authService)
    {
        var token  = context.Request.Cookies["session_id"];
        authService.DeleteSession(token);
        
        context.Response.Cookies.Delete("session_id");
        return Results.Ok(new {success = true});
    }
    
    
    private static async Task<IResult> CreateUserHandler(CreateUserRequest request, DBContext context, CryptoService crypto)
    {
        var settings = await context.SystemSettings
            .SingleAsync(s => s.Id == 1);
        
        Invite? invite = null;
        
        if (settings.AllowRegistration == false)
        {
            if (request.InviteId == null)
                return Results.Json(new { success = false, message = "Registration is closed. An invite is required." }, statusCode: 403);
                
            invite = await context.Invites.FirstOrDefaultAsync(i => i.Id == request.InviteId);
            if (invite == null || DateTime.UtcNow > invite.ExpireAt)
                return Results.Json(new { success = false, message = "Invalid or expired invite." }, statusCode: 403);
        }
        
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUser != null)
            return Results.Conflict(new { success = false, message = "User already exists." });

        var user = new User()
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            HashPassword = crypto.GetArgon2id(request.Password),
        };
        
        context.Users.Add(user);
        
        if (invite != null)
        {
            context.Invites.Remove(invite);
        }
        
        await context.SaveChangesAsync();
        
        return Results.Ok(new { success = true });
    }
    
    
    
    private static async Task<IResult> AuthorizeHandler(
        AuthorizeRequest request,
        DBContext dbContext, 
        CryptoService crypto, 
        IWebHostEnvironment env,
        AuthService authService,
        HttpContext context)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Results.Unauthorized();

        if (!crypto.VerifyArgon2id(request.Password, user.HashPassword))
        {
            return Results.Unauthorized();
        }

        var token = authService.CreateSession(user.Id, user.Username);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        
        context.Response.Cookies.Append("session_id", token, cookieOptions);
    
        return Results.Ok(new { success = true });
    }

    
    
}
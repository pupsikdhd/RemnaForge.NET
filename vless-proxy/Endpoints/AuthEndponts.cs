using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using vless_proxy.Auth;
using vless_proxy.Contracts.Request.Auth;
using vless_proxy.Contracts.Request.TOTP;
using vless_proxy.Contracts.Response.Auth;
using vless_proxy.Crypto;
using vless_proxy.Data;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class AuthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapPost("/login", AuthorizeHandler).RequireRateLimiting("auth-limiter");
        group.MapPost("/verify-totp", VerifyTOTPHandler).RequireRateLimiting("auth-limiter");
        group.MapPost("/create", CreateUserHandler).RequireRateLimiting("auth-limiter");
        group.MapPost("/logout", LogoutHandler).AddEndpointFilter<SessionAuthFilter>();
    }

    private static IResult LogoutHandler(HttpContext context, AuthService authService)
    {
        var token = context.Request.Cookies[GlobalVariables.AuthCookie];
        authService.DeleteSession(token);
        
        context.Response.Cookies.Delete(GlobalVariables.AuthCookie);
        return Results.Ok(new { success = true });
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
        IMemoryCache cache,
        HttpContext context)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Results.Json(new LoginResponse { Success = false, Message = "Invalid username or password" }, statusCode: 401);

        if (!crypto.VerifyArgon2id(request.Password, user.HashPassword))
        {
            return Results.Json(new LoginResponse { Success = false, Message = "Invalid username or password" }, statusCode: 401);
        }

        if (!string.IsNullOrEmpty(user.TOTPSecret))
        {
            var totpToken = crypto.GetCode(64);
            var totpSession = new TOTPSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            cache.Set(CacheKeys.TOTPSession + totpToken, totpSession, TimeSpan.FromMinutes(5));

            return Results.Ok(new LoginResponse
            {
                Success = false,
                RequiresTotp = true,
                TotpToken = totpToken,
                Message = "TOTP verification required."
            });
        }

        var token = authService.CreateSession(user.Id, user.Username);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        
        context.Response.Cookies.Append(GlobalVariables.AuthCookie, token, cookieOptions);
    
        return Results.Ok(new LoginResponse
        {
            Success = true,
            Message = "Logged in successfully."
        });
    }

    private static async Task<IResult> VerifyTOTPHandler(
        TOTPVerifyRequest request,
        DBContext dbContext,
        IMemoryCache cache,
        CryptoService crypto,
        IWebHostEnvironment env,
        AuthService authService,
        HttpContext context)
    {
        var cacheKey = CacheKeys.TOTPSession + request.TotpToken;
        if (!cache.TryGetValue(cacheKey, out TOTPSession? totpSession) || totpSession == null)
        {
            return Results.Json(new LoginResponse
            {
                Success = false,
                Message = "Session expired or invalid. Please login again."
            }, statusCode: 400);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == totpSession.UserId);
        if (user == null)
        {
            return Results.NotFound();
        }

        var decryptedSecret = crypto.Decrypt(user.TOTPSecret);
        if (!OTP_Module.Validate(request.Code, decryptedSecret))
        {
            return Results.Json(new LoginResponse
            {
                Success = false,
                Message = "Invalid verification code."
            }, statusCode: 400);
        }

        // Remove the temporary TOTP session from cache
        cache.Remove(cacheKey);

        // Standard session setup
        var token = authService.CreateSession(user.Id, user.Username);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        
        context.Response.Cookies.Append(GlobalVariables.AuthCookie, token, cookieOptions);
    
        return Results.Ok(new LoginResponse
        {
            Success = true,
            Message = "Logged in successfully."
        });
    }
}
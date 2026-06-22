using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using vless_proxy.Contracts.Response.Auth;
using vless_proxy.Contracts.Request.TOTP;
using vless_proxy.Crypto;
using vless_proxy.Data;
using vless_proxy.Filters;
using vless_proxy.Models;

namespace vless_proxy.Endpoints;

public class TOTPEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/totp").AddEndpointFilter<SessionAuthFilter>();

        group.MapGet("/", TOTPStatusHandler);
        group.MapGet("/generate", GenerateSecretHandler);
        group.MapPost("/enable", EnableTOTPHandler);
        group.MapPost("/disable", DisableTOTPHandler);
    }

    private static async Task<IResult> TOTPStatusHandler(HttpContext httpContext, DBContext context)
    {
        var session = httpContext.Items[GlobalVariables.SessionContext] as Session;
        if (session == null)
            return Results.Unauthorized();
        
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user == null) 
            return Results.NotFound();
        
        return Results.Ok(new TOTPStatusResponse { Enabled = !string.IsNullOrEmpty(user.TOTPSecret) });
    }

    private static async Task<IResult> GenerateSecretHandler(HttpContext httpContext, IMemoryCache cache)
    {
        var session = httpContext.Items[GlobalVariables.SessionContext] as Session;
        if (session == null)
            return Results.Unauthorized();
        
        var key = CacheKeys.OTP + session.Id;
        if (!cache.TryGetValue(key, out string? secret) || string.IsNullOrEmpty(secret))
        {
            secret = OTP_Module.GenerateSecret();
            cache.Set(key, secret, TimeSpan.FromMinutes(5));
        }

        var qrCodeUri = $"otpauth://totp/vless-proxy:{Uri.EscapeDataString(session.UserName)}?secret={secret}&issuer=vless-proxy";
        
        return Results.Ok(new TOTPSecretResponse 
        { 
            Secret = secret,
            QrCodeUri = qrCodeUri
        });
    }

    private static async Task<IResult> EnableTOTPHandler(
        TOTPEnableRequest request, 
        HttpContext httpContext, 
        DBContext context, 
        IMemoryCache cache, 
        CryptoService cryptoService)
    {
        var session = httpContext.Items[GlobalVariables.SessionContext] as Session;
        if (session == null)
            return Results.Unauthorized();

        var key = CacheKeys.OTP + session.Id;
        if (!cache.TryGetValue(key, out string? cachedSecret) || string.IsNullOrEmpty(cachedSecret))
        {
            return Results.BadRequest(new { success = false, message = "Secret expired or not generated. Please generate secret first." });
        }

        if (!OTP_Module.Validate(request.Code, cachedSecret))
        {
            return Results.BadRequest(new { success = false, message = "Invalid verification code." });
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user == null)
            return Results.NotFound();

        user.TOTPSecret = cryptoService.Encrypt(cachedSecret);
        await context.SaveChangesAsync();

        cache.Remove(key);

        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> DisableTOTPHandler(
        TOTPDisableRequest request, 
        HttpContext httpContext, 
        DBContext context, 
        CryptoService cryptoService)
    {
        var session = httpContext.Items[GlobalVariables.SessionContext] as Session;
        if (session == null)
            return Results.Unauthorized();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user == null)
            return Results.NotFound();

        if (string.IsNullOrEmpty(user.TOTPSecret))
        {
            return Results.BadRequest(new { success = false, message = "TOTP is not enabled." });
        }

        var decryptedSecret = cryptoService.Decrypt(user.TOTPSecret);
        if (!OTP_Module.Validate(request.Code, decryptedSecret))
        {
            return Results.BadRequest(new { success = false, message = "Invalid verification code." });
        }

        user.TOTPSecret = string.Empty;
        await context.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }
}
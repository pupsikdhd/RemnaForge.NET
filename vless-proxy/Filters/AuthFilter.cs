using Microsoft.EntityFrameworkCore;
using vless_proxy.Auth;
using vless_proxy.Data;

namespace vless_proxy.Filters;

public class SessionAuthFilter : IEndpointFilter
{
    private readonly AuthService _authService;
    
    public SessionAuthFilter(AuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Cookies.TryGetValue(GlobalVariables.AuthCookie, out var token) || string.IsNullOrEmpty(token))
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }
        
        var session = _authService.GetSession(token);
        if (session == null)
        {
            return Results.Json(new { error = "Invalid session" }, statusCode: 401);
        }
        
        context.HttpContext.Items[GlobalVariables.SessionContext] = session;
        
        return await next(context);
    }
}

public class AdminAuthFilter : IEndpointFilter
{
    private readonly AuthService _authService;
    
    public AdminAuthFilter(AuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Check Session first
        if (context.HttpContext.Request.Cookies.TryGetValue(GlobalVariables.AuthCookie, out var token) && !string.IsNullOrEmpty(token))
        {
            var session = _authService.GetSession(token);
            if (session != null)
            {
                context.HttpContext.Items[GlobalVariables.SessionContext] = session;
                return await next(context);
            }
        }

        // Check API Key
        if (context.HttpContext.Request.Headers.TryGetValue(GlobalVariables.ApiKeyHeader, out var apiKey) && !string.IsNullOrEmpty(apiKey))
        {
            var dbContext = context.HttpContext.RequestServices.GetService<DBContext>();
            if (dbContext != null)
            {
                var keyExists = await dbContext.ApiKeys.AnyAsync(k => k.Key == apiKey.ToString());
                if (keyExists)
                {
                    return await next(context);
                }
            }
        }

        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
    }
}
using vless_proxy.Auth;

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
        if (!context.HttpContext.Request.Cookies.TryGetValue("session_id", out var token) || string.IsNullOrEmpty(token))
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }
        
        var session = _authService.GetSession(token);
        if (session == null)
        {
            return Results.Json(new { error = "Invalid session" }, statusCode: 401);
        }
        
        context.HttpContext.Items["UserSession"] = session;
        
        return await next(context);
    }
}
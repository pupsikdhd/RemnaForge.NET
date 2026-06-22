using Microsoft.Extensions.Caching.Memory;
using vless_proxy.Crypto;
using vless_proxy.Data;
using vless_proxy.Models;

namespace vless_proxy.Auth;

public class AuthService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;
    private readonly CryptoService _crypto;

    public AuthService(IMemoryCache cache, ILogger<AuthService> logger, CryptoService crypto)
    {
        _cache = cache;
        _logger = logger;
        _crypto = crypto;
    }
    
    public string CreateSession(Guid userId, string UserName)
    {
        var sessionToken = _crypto.GetCode(64);
        var session = new Session()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = UserName,
            CreatedAt =  DateTime.UtcNow,
        };
        
        _cache.Set(CacheKeys.Session + sessionToken, session, TimeSpan.FromHours(12));
        
        _logger.LogInformation($"Session created: {userId}");
        return sessionToken;
    }

    public void DeleteSession(string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        _cache.Remove(CacheKeys.Session + token);
        _logger.LogInformation($"Session deleted: {token}");
    }

    public Session? GetSession(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        return _cache.Get<Session>(CacheKeys.Session + token);
    }
    
    
}
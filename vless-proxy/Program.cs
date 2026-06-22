using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using vless_proxy;
using vless_proxy.Auth;
using vless_proxy.Crypto;
using vless_proxy.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddPolicy("auth-limiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0 
            });
    });

    options.AddPolicy("public-sub-limiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0 
            });
    });
});

builder.Services.AddDbContext<DBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultDatabase")));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("", client => { })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | 
                                 System.Net.DecompressionMethods.Deflate | 
                                 System.Net.DecompressionMethods.Brotli
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
builder.Services.AddSingleton<OTP_Module>();
builder.Services.AddSingleton<CryptoService>();
builder.Services.AddScoped<AuthService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseDefaultFiles();
app.UseHsts();


if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCorsPolicy");
}

app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DBContext>();
    db.Database.EnsureCreated();
    
    if (!db.SystemSettings.Any())
    {
        db.SystemSettings.Add(new SystemSettings
        {
            Id = 1
        });

        await db.SaveChangesAsync();
    }
}

var modules = typeof(Program).Assembly.GetTypes()
    .Where(t => typeof(ICarterModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

foreach (var module in modules)
{
    var instance = (ICarterModule)Activator.CreateInstance(module)!;
    instance.AddRoutes(app);
}
app.MapFallbackToFile("index.html");
app.UseForwardedHeaders();
app.Run();

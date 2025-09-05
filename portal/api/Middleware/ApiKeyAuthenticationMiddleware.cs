using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using API.Services;

namespace API.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly ApiKeyAuthenticationOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        ApiKeyAuthenticationOptions options,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _options = options;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for OPTIONS requests (CORS preflight)
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context);
            return;
        }
        
        // Skip authentication for health checks, swagger, and other public endpoints
        if (IsPublicEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var apiKey = ExtractApiKey(context.Request);

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API key missing for request to {Path} from {IP}", 
                context.Request.Path, context.Connection.RemoteIpAddress);
            await WriteUnauthorizedResponse(context, "API key is required");
            return;
        }

        _logger.LogDebug("API key received for request to {Path}: {ApiKey}", 
            context.Request.Path, apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...");

        var (isValid, userInfo) = ValidateApiKey(apiKey);

        if (!isValid)
        {
            _logger.LogWarning("Invalid API key '{ApiKey}' used for request to {Path} from {IP}", 
                apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...", 
                context.Request.Path, 
                context.Connection.RemoteIpAddress);
            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // Set up authentication claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userInfo!.Name),
            new Claim(ClaimTypes.NameIdentifier, userInfo.Id),
            new Claim("ApiKeyId", userInfo.ApiKeyId),
            new Claim("UserType", userInfo.UserType)
        };

        // Add role claims
        foreach (var role in userInfo.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        var principal = new ClaimsPrincipal(identity);

        context.User = principal;

        // Add user context to request headers for logging
        context.Items["UserId"] = userInfo.Id;
        context.Items["UserName"] = userInfo.Name;
        context.Items["ApiKeyId"] = userInfo.ApiKeyId;

        _logger.LogDebug("Authenticated user {UserName} ({UserId}) for request to {Path}", 
            userInfo.Name, userInfo.Id, context.Request.Path);

        await _next(context);
    }

    private bool IsPublicEndpoint(PathString path)
    {
        var publicPaths = new[]
        {
            "/health",
            "/swagger",
            "/api/health",
            "/api/agent/register", // Only registration endpoint is public for agents
            "/api/agent/heartbeat", // Heartbeat endpoint for agents
            "/hangfire", // May want to secure this in production
            "/"
        };

        return publicPaths.Any(publicPath => 
            path.StartsWithSegments(publicPath, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractApiKey(HttpRequest request)
    {
        // Log all headers for debugging
        _logger.LogDebug("Headers received for {Path}:", request.Path);
        foreach (var header in request.Headers)
        {
            // Don't log sensitive headers in full
            if (header.Key.Contains("Authorization", StringComparison.OrdinalIgnoreCase) || 
                header.Key.Contains("API", StringComparison.OrdinalIgnoreCase))
            {
                var value = header.Value.FirstOrDefault();
                if (!string.IsNullOrEmpty(value) && value.Length > 10)
                {
                    _logger.LogDebug("  {HeaderKey}: {HeaderValue}...", header.Key, value.Substring(0, 10));
                }
                else
                {
                    _logger.LogDebug("  {HeaderKey}: {HeaderValue}", header.Key, value);
                }
            }
            else
            {
                _logger.LogDebug("  {HeaderKey}: {HeaderValue}", header.Key, header.Value);
            }
        }
        
        // Try to get API key from multiple sources
        
        // 1. Authorization header (Bearer token)
        if (request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }
        }

        // 2. X-API-Key header
        if (request.Headers.ContainsKey("X-API-Key"))
        {
            var apiKey = request.Headers["X-API-Key"].FirstOrDefault();
            _logger.LogDebug("Found X-API-Key header: {ApiKey}", apiKey?.Substring(0, Math.Min(10, apiKey?.Length ?? 0)) + "...");
            return apiKey;
        }

        // 3. Query parameter (less secure, mainly for development)
        if (request.Query.ContainsKey("apiKey"))
        {
            return request.Query["apiKey"].FirstOrDefault();
        }

        _logger.LogDebug("No API key found in request headers or query params");
        return null;
    }

    private (bool IsValid, ApiKeyUserInfo? UserInfo) ValidateApiKey(string apiKey)
    {
        // Try to get ApiKeyService from DI
        using (var scope = _serviceProvider.CreateScope())
        {
            var apiKeyService = scope.ServiceProvider.GetService<IApiKeyService>();
            if (apiKeyService != null)
            {
                var userInfo = apiKeyService.GetUserInfo(apiKey);
                if (userInfo != null && userInfo.IsActive)
                {
                    return (true, userInfo);
                }
            }
        }
        
        // Fallback to configured API keys from appsettings
        if (_options.ValidApiKeys.TryGetValue(apiKey, out var configuredUserInfo))
        {
            return (true, configuredUserInfo);
        }

        return (false, null);
    }

    private async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = "Unauthorized",
            message = message,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}

public class ApiKeyAuthenticationOptions
{
    public Dictionary<string, ApiKeyUserInfo> ValidApiKeys { get; set; } = new();
    public bool RequireHttps { get; set; } = true;
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(30);
}

public class ApiKeyUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ApiKeyId { get; set; } = string.Empty;
    public string UserType { get; set; } = "User";
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

// Extension method to register the middleware
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ApiKeyAuthenticationOptions();
        configuration.GetSection("ApiKeyAuthentication").Bind(options);

        // Add some default API keys if none configured
        if (!options.ValidApiKeys.Any())
        {
            var defaultApiKey = configuration.GetValue<string>("DefaultApiKey") ?? "dev-api-key-12345";
            options.ValidApiKeys[defaultApiKey] = new ApiKeyUserInfo
            {
                Id = "default-user",
                Name = "Default User",
                ApiKeyId = "default",
                UserType = "Administrator",
                Roles = new List<string> { "Admin", "User" },
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        services.AddSingleton(options);
        return services;
    }

    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
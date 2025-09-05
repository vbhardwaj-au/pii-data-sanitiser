using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using API.Middleware;

namespace API.Services;

public interface IApiKeyService
{
    Task<bool> ValidateApiKeyAsync(string apiKey);
    Task StoreApiKeyAsync(string apiKey, Guid agentId, string agentName);
    Task RevokeApiKeyAsync(string apiKey);
    Task<string?> GetApiKeyForAgentAsync(Guid agentId);
    ApiKeyUserInfo? GetUserInfo(string apiKey);
}

public class ApiKeyService : IApiKeyService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ApiKeyService> _logger;
    private readonly ConcurrentDictionary<string, ApiKeyUserInfo> _apiKeys;
    
    public ApiKeyService(
        IMemoryCache cache,
        ILogger<ApiKeyService> logger)
    {
        _cache = cache;
        _logger = logger;
        _apiKeys = new ConcurrentDictionary<string, ApiKeyUserInfo>();
        
        // Add default admin keys
        var defaultKey = "admin-api-key-12345";
        _apiKeys[defaultKey] = new ApiKeyUserInfo
        {
            Id = "admin",
            Name = "Administrator",
            ApiKeyId = "admin-key",
            UserType = "Administrator",
            Roles = new List<string> { "Admin", "User" },
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        // Add portal admin key (used by web UI)
        var portalKey = "portal-admin-key-2024";
        _apiKeys[portalKey] = new ApiKeyUserInfo
        {
            Id = "portal-admin",
            Name = "Portal Administrator",
            ApiKeyId = "portal-key",
            UserType = "Administrator",
            Roles = new List<string> { "Admin", "User", "Analyst" },
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }
    
    public Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return Task.FromResult(false);
            
        // Check in-memory dictionary
        if (_apiKeys.ContainsKey(apiKey))
        {
            return Task.FromResult(_apiKeys[apiKey].IsActive);
        }
        
        // Check cache
        if (_cache.TryGetValue($"apikey:{apiKey}", out ApiKeyUserInfo? cachedInfo))
        {
            return Task.FromResult(cachedInfo?.IsActive ?? false);
        }
        
        return Task.FromResult(false);
    }
    
    public Task StoreApiKeyAsync(string apiKey, Guid agentId, string agentName)
    {
        var userInfo = new ApiKeyUserInfo
        {
            Id = agentId.ToString(),
            Name = agentName,
            ApiKeyId = $"agent-{agentId}",
            UserType = "Agent",
            Roles = new List<string> { "Agent" },
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        // Store in memory
        _apiKeys[apiKey] = userInfo;
        
        // Store in cache with expiration
        _cache.Set($"apikey:{apiKey}", userInfo, TimeSpan.FromDays(30));
        
        _logger.LogInformation("Stored API key for agent {AgentName} ({AgentId})", agentName, agentId);
        
        return Task.CompletedTask;
    }
    
    public Task RevokeApiKeyAsync(string apiKey)
    {
        if (_apiKeys.TryGetValue(apiKey, out var userInfo))
        {
            userInfo.IsActive = false;
        }
        
        _cache.Remove($"apikey:{apiKey}");
        
        _logger.LogInformation("Revoked API key {ApiKey}", apiKey.Substring(0, 10) + "...");
        
        return Task.CompletedTask;
    }
    
    public Task<string?> GetApiKeyForAgentAsync(Guid agentId)
    {
        // Find API key for agent from in-memory dictionary
        var apiKeyEntry = _apiKeys.FirstOrDefault(kvp => 
            kvp.Value.Id == agentId.ToString());
        
        if (!string.IsNullOrEmpty(apiKeyEntry.Key))
        {
            return Task.FromResult<string?>(apiKeyEntry.Key);
        }
        
        // Check cache entries (this is less efficient but covers cached keys)
        // In production, you'd want to maintain a reverse lookup dictionary
        return Task.FromResult<string?>(null);
    }
    
    public ApiKeyUserInfo? GetUserInfo(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;
            
        // Check in-memory dictionary
        if (_apiKeys.TryGetValue(apiKey, out var userInfo))
        {
            return userInfo;
        }
        
        // Check cache
        if (_cache.TryGetValue($"apikey:{apiKey}", out ApiKeyUserInfo? cachedInfo))
        {
            return cachedInfo;
        }
        
        return null;
    }
}
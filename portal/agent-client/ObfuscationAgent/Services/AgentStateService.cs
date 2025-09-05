using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ObfuscationAgent.Services;

public interface IAgentStateService
{
    Task<Guid?> GetAgentIdAsync();
    Task SetAgentIdAsync(Guid agentId);
    Task<string?> GetApiKeyAsync();
    Task SetApiKeyAsync(string apiKey);
    Task ClearCredentialsAsync();
    Task<int> GetAvailableCapacityAsync();
    Task UpdateJobCountAsync(int activeJobs, int completedJobs, int failedJobs);
    Task<AgentStatistics> GetStatisticsAsync();
}

public class AgentStateService : IAgentStateService
{
    private readonly string _stateFilePath;
    private readonly ILogger<AgentStateService> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private AgentState? _currentState;

    public AgentStateService(ILogger<AgentStateService> logger)
    {
        _logger = logger;
        _stateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObfuscationAgent",
            "agent-state.json"
        );
        
        EnsureDirectoryExists();
        LoadState().Wait();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task LoadState()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = await File.ReadAllTextAsync(_stateFilePath);
                _currentState = JsonSerializer.Deserialize<AgentState>(json);
            }
            else
            {
                _currentState = new AgentState();
                await SaveState();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent state");
            _currentState = new AgentState();
        }
    }

    private async Task SaveState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentState, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save agent state");
        }
    }

    public async Task<Guid?> GetAgentIdAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            return _currentState?.AgentId;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SetAgentIdAsync(Guid agentId)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState == null)
            {
                _currentState = new AgentState();
            }
            _currentState.AgentId = agentId;
            await SaveState();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<string?> GetApiKeyAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            return _currentState?.ApiKey;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState == null)
            {
                _currentState = new AgentState();
            }
            _currentState.ApiKey = apiKey;
            await SaveState();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task ClearCredentialsAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState != null)
            {
                _currentState.AgentId = null;
                _currentState.ApiKey = null;
                await SaveState();
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<int> GetAvailableCapacityAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState == null)
            {
                return 0;
            }
            
            var maxJobs = _currentState.MaxConcurrentJobs;
            var activeJobs = _currentState.ActiveJobs;
            return Math.Max(0, maxJobs - activeJobs);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task UpdateJobCountAsync(int activeJobs, int completedJobs, int failedJobs)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState == null)
            {
                _currentState = new AgentState();
            }
            
            _currentState.ActiveJobs = activeJobs;
            _currentState.CompletedJobs = completedJobs;
            _currentState.FailedJobs = failedJobs;
            _currentState.LastUpdated = DateTime.UtcNow;
            
            await SaveState();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<AgentStatistics> GetStatisticsAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_currentState == null)
            {
                return new AgentStatistics();
            }
            
            return new AgentStatistics
            {
                ActiveJobs = _currentState.ActiveJobs,
                CompletedJobs = _currentState.CompletedJobs,
                FailedJobs = _currentState.FailedJobs,
                TotalJobs = _currentState.CompletedJobs + _currentState.FailedJobs,
                LastUpdated = _currentState.LastUpdated
            };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private class AgentState
    {
        public Guid? AgentId { get; set; }
        public string? ApiKey { get; set; }
        public int MaxConcurrentJobs { get; set; } = 1;  // Only handle one job at a time
        public int ActiveJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

public class AgentStatistics
{
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int TotalJobs { get; set; }
    public DateTime LastUpdated { get; set; }
}
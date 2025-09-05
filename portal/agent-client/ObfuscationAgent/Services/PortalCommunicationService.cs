using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfuscationAgent.Configuration;
using ObfuscationAgent.Models;
using Contracts.Models;
using Contracts.DTOs;

namespace ObfuscationAgent.Services;

public interface IPortalCommunicationService
{
    Task<Models.AgentRegistrationResponse?> RegisterAgentAsync(Models.AgentRegistrationRequest request);
    Task<Models.AgentHeartbeatResponse?> SendHeartbeatAsync(Guid agentId, Models.AgentHeartbeatRequest request);
    Task<Models.JobAssignment?> RequestJobAsync();
    Task<bool> UpdateJobStatusAsync(Guid jobId, Models.JobStatusUpdate update);
    Task<bool> UpdateJobProgressAsync(Guid jobId, JobProgressUpdate progress);
    Task<bool> UploadJobResultsAsync(Guid jobId, string resultsData);
    Task<string?> GetMappingDownloadUrlAsync(Guid jobId);
    Task<string?> GetJobConfigurationAsync(Guid jobId);
}

public class PortalCommunicationService : IPortalCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<PortalCommunicationService> _logger;
    private readonly PortalConfiguration _portalConfig;
    private readonly JsonSerializerOptions _jsonOptions;

    public PortalCommunicationService(
        HttpClient httpClient,
        IAgentStateService agentState,
        IOptions<PortalConfiguration> portalConfig,
        ILogger<PortalCommunicationService> logger)
    {
        _httpClient = httpClient;
        _agentState = agentState;
        _logger = logger;
        _portalConfig = portalConfig.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<Models.AgentRegistrationResponse?> RegisterAgentAsync(Models.AgentRegistrationRequest request)
    {
        try
        {
            _logger.LogInformation("Registering agent with portal at {BaseUrl}", _httpClient.BaseAddress);
            
            var response = await _httpClient.PostAsJsonAsync(_portalConfig.RegistrationEndpoint, request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Registration response content: {Content}", responseContent);
                
                try
                {
                    // Try to deserialize into the contracts DTO first
                    var apiResponse = JsonSerializer.Deserialize<Contracts.DTOs.AgentRegistrationResponse>(responseContent, _jsonOptions);
                    
                    if (apiResponse != null)
                    {
                        // Map to the local model
                        var result = new Models.AgentRegistrationResponse
                        {
                            Success = apiResponse.Success,
                            AgentId = apiResponse.AgentId,
                            ApiKey = apiResponse.ApiKey,
                            Message = apiResponse.ErrorMessage ?? "Registration successful"
                        };
                        
                        if (result.Success)
                        {
                            _logger.LogInformation("Agent registered successfully with ID: {AgentId}", result.AgentId);
                            
                            // Store credentials in agent state
                            await _agentState.SetAgentIdAsync(result.AgentId);
                            await _agentState.SetApiKeyAsync(result.ApiKey);
                        }
                        
                        return result;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize registration response: {Content}", responseContent);
                }
                
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Try to read the conflict response
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Agent registration conflict. Response: {Response}", content);
                
                // Parse the conflict response if it contains agent info
                try
                {
                    var conflictResponse = await response.Content.ReadFromJsonAsync<Models.AgentRegistrationResponse>(_jsonOptions);
                    return conflictResponse;
                }
                catch
                {
                    // If we can't parse it, return null
                    return null;
                }
            }
            
            _logger.LogError("Failed to register agent. Status: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent with portal");
            return null;
        }
    }

    public async Task<Models.AgentHeartbeatResponse?> SendHeartbeatAsync(Guid agentId, Models.AgentHeartbeatRequest request)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("No API key found. Agent may need to re-register.");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var endpoint = string.Format(_portalConfig.HeartbeatEndpoint, agentId);
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Models.AgentHeartbeatResponse>(_jsonOptions);
                _logger.LogDebug("Heartbeat sent successfully. New jobs: {JobCount}", result?.NewJobs?.Count ?? 0);
                return result;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Heartbeat unauthorized. Agent may need to re-register.");
                await _agentState.ClearCredentialsAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat to portal");
            return null;
        }
    }

    public async Task<Models.JobAssignment?> RequestJobAsync()
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return null;
            }

            var request = new Models.JobRequest
            {
                MaxConcurrentJobs = await _agentState.GetAvailableCapacityAsync()
            };

            // Use POST for polling endpoint with agent ID in header
            var agentId = await _agentState.GetAgentIdAsync();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Add("X-Agent-Id", agentId.ToString());
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ObfuscationAgent/1.0");
            
            var response = await _httpClient.PostAsync("/api/agent/jobs/poll", null);

            if (response.IsSuccessStatusCode)
            {
                // The API returns the job details directly, not wrapped in JobAssignmentResponse
                var jobDetails = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(jobDetails))
                {
                    // Parse the response to get job details
                    var jobData = JsonSerializer.Deserialize<JsonElement>(jobDetails, _jsonOptions);
                    
                    // ProductId is a GUID from the API
                    var productId = jobData.GetProperty("productId").GetGuid();
                    
                    var job = new Models.JobAssignment
                    {
                        JobId = jobData.GetProperty("jobId").GetGuid(),
                        JobName = jobData.GetProperty("jobName").GetString() ?? "",
                        ProductId = productId,
                        ProductName = "Product-" + productId.ToString()[..8], // Use first 8 chars of GUID
                        ConnectionString = "", // Will be filled from configuration
                        MappingBlobUrl = jobData.TryGetProperty("configurationUrl", out var configUrl) ? configUrl.GetString() ?? "" : "",
                        Priority = (JobPriority)(jobData.TryGetProperty("priority", out var priority) ? priority.GetInt32() : 1)
                    };
                    
                    _logger.LogInformation("Received job assignment: {JobId} - {JobName}", 
                        job.JobId, job.JobName);
                    return job;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                // No jobs available
                _logger.LogDebug("No jobs available from portal");
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting job from portal");
            return null;
        }
    }

    public async Task<bool> UpdateJobStatusAsync(Guid jobId, Models.JobStatusUpdate update)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsJsonAsync($"/api/agent/jobs/{jobId}/status", update);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Job {JobId} status updated to {Status}", jobId, update.Status);
                return true;
            }

            _logger.LogWarning("Failed to update job status. Status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job status for {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> UpdateJobProgressAsync(Guid jobId, Models.JobProgressUpdate progress)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsJsonAsync($"/api/agent/jobs/{jobId}/progress", progress);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Job {JobId} progress updated: {Progress}%", jobId, progress.ProgressPercentage);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job progress for {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> UploadJobResultsAsync(Guid jobId, string resultsData)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var upload = new Models.JobResultsUpload
            {
                ResultsData = resultsData,
                Summary = $"Job {jobId} completed successfully",
                Format = "json"
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/agent/jobs/{jobId}/results", upload);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Job {JobId} results uploaded successfully", jobId);
                return true;
            }

            _logger.LogWarning("Failed to upload job results. Status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading job results for {JobId}", jobId);
            return false;
        }
    }

    public async Task<string?> GetMappingDownloadUrlAsync(Guid jobId)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.GetAsync($"/api/agent/jobs/{jobId}/mapping");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Models.MappingUrlResponse>(_jsonOptions);
                return result?.MappingUrl;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mapping download URL for {JobId}", jobId);
            return null;
        }
    }

    public async Task<string?> GetJobConfigurationAsync(Guid jobId)
    {
        try
        {
            var apiKey = await _agentState.GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.GetAsync($"/api/agent/job/{jobId}/configuration");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse the API response to extract just the configuration object
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;
                
                if (root.TryGetProperty("configuration", out var configElement))
                {
                    // Return just the configuration object as JSON string
                    return JsonSerializer.Serialize(configElement, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                }

                _logger.LogError("Configuration property not found in API response");
                return null;
            }

            _logger.LogError("Failed to get job configuration. Status: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job configuration for {JobId}", jobId);
            return null;
        }
    }
}
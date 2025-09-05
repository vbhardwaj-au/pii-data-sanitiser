using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using Contracts.DTOs;
using Contracts.Models;

namespace Web.Controllers;

public class AgentsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AgentsController> logger)
    {
        _httpClient = httpClientFactory.CreateClient("API");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/agent");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var agents = JsonSerializer.Deserialize<List<ObfuscationAgent>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                ViewBag.TotalAgents = agents?.Count ?? 0;
                ViewBag.OnlineAgents = agents?.Count(a => a.Status == AgentStatus.Online || a.Status == AgentStatus.Busy) ?? 0;
                ViewBag.BusyAgents = agents?.Count(a => a.Status == AgentStatus.Busy) ?? 0;
                ViewBag.OfflineAgents = agents?.Count(a => a.Status == AgentStatus.Offline) ?? 0;
                
                return View(agents ?? new List<ObfuscationAgent>());
            }
            
            _logger.LogWarning("Failed to fetch agents. Status: {StatusCode}", response.StatusCode);
            return View(new List<ObfuscationAgent>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching agents");
            ViewBag.ErrorMessage = "Failed to load agents. Please try again later.";
            return View(new List<ObfuscationAgent>());
        }
    }

    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/agent/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var agent = JsonSerializer.Deserialize<ObfuscationAgent>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (agent != null)
                {
                    // Get agent jobs
                    var jobsResponse = await _httpClient.GetAsync($"/api/agent/{id}/jobs");
                    if (jobsResponse.IsSuccessStatusCode)
                    {
                        var jobsJson = await jobsResponse.Content.ReadAsStringAsync();
                        var jobs = JsonSerializer.Deserialize<List<ObfuscationJob>>(jobsJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        ViewBag.Jobs = jobs ?? new List<ObfuscationJob>();
                    }
                    
                    return View(agent);
                }
            }
            
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching agent details for {AgentId}", id);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(Guid id, AgentStatus status)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"api/agent/{id}/status?status={status}", null);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Agent status updated successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update agent status";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent status for {AgentId}", id);
            TempData["ErrorMessage"] = "An error occurred while updating agent status";
        }
        
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Health()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/agent/health");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var health = JsonSerializer.Deserialize<AgentHealthDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return View(health);
            }
            
            return View(new AgentHealthDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching agent health status");
            return View(new AgentHealthDto());
        }
    }

    public IActionResult ProductAssignments()
    {
        // This page shows agent-product assignments and allows pipeline triggering
        return View();
    }
}

public class AgentHealthDto
{
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public int BusyAgents { get; set; }
    public int OfflineAgents { get; set; }
    public double AverageCpuUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public int TotalActiveJobs { get; set; }
    public int TotalCompletedJobs { get; set; }
    public int TotalFailedJobs { get; set; }
    public DateTime LastHealthCheck { get; set; }
}
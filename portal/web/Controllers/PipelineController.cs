using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using Contracts.DTOs;

namespace Web.Controllers;

public class PipelineController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration,
        ILogger<PipelineController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Main pipeline dashboard showing all products and their pipeline status
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            // Get dashboard overview
            var dashboardResponse = await client.GetAsync("/api/PipelineMonitoring/dashboard");
            if (dashboardResponse.IsSuccessStatusCode)
            {
                var content = await dashboardResponse.Content.ReadAsStringAsync();
                var dashboard = JsonSerializer.Deserialize<DashboardOverviewDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return View(dashboard);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline dashboard");
            ViewBag.Error = "Unable to load dashboard. Please try again later.";
        }
        
        return View(new DashboardOverviewDto());
    }

    /// <summary>
    /// Product pipeline configuration view - shows all stages and scripts
    /// </summary>
    public async Task<IActionResult> Configure(Guid productId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            // Get pipeline configuration overview
            var response = await client.GetAsync($"/api/PipelineConfiguration/product/{productId}/overview");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var configuration = JsonSerializer.Deserialize<PipelineConfigurationOverviewDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                ViewBag.ProductId = productId;
                ViewBag.ApiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:7001/";
                
                return View(configuration);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline configuration for product {ProductId}", productId);
            ViewBag.Error = "Unable to load pipeline configuration. Please try again later.";
        }
        
        return View(null);
    }

    /// <summary>
    /// Script editor view for creating/editing pipeline scripts
    /// </summary>
    public async Task<IActionResult> EditScript(Guid productId, Guid stageId, Guid? scriptId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            if (scriptId.HasValue)
            {
                // Load existing script
                var response = await client.GetAsync($"/api/PipelineScript/{scriptId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var script = JsonSerializer.Deserialize<PipelineStageScriptDto>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    ViewBag.ProductId = productId;
                    ViewBag.StageId = stageId;
                    ViewBag.ScriptId = scriptId;
                    
                    return View(script);
                }
            }
            else
            {
                // New script
                ViewBag.ProductId = productId;
                ViewBag.StageId = stageId;
                
                return View(new PipelineStageScriptDto
                {
                    ProductId = productId,
                    PipelineStageId = stageId,
                    IsEnabled = true,
                    TimeoutMinutes = 30,
                    OnFailureAction = "Stop"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading script editor");
            ViewBag.Error = "Unable to load script editor. Please try again later.";
        }
        
        return View(null);
    }

    /// <summary>
    /// Pipeline execution monitoring view
    /// </summary>
    public async Task<IActionResult> Monitor(Guid? executionId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            if (executionId.HasValue)
            {
                // Get specific execution details
                var response = await client.GetAsync($"/api/PipelineMonitoring/executions/{executionId}/details");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var progress = JsonSerializer.Deserialize<PipelineProgressDto>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    ViewBag.ExecutionId = executionId;
                    return View("ExecutionDetails", progress);
                }
            }
            else
            {
                // Get current executions
                var response = await client.GetAsync("/api/PipelineMonitoring/executions/current");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var executions = JsonSerializer.Deserialize<List<PipelineExecutionDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return View(executions);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline monitoring");
            ViewBag.Error = "Unable to load monitoring data. Please try again later.";
        }
        
        return View(new List<PipelineExecutionDto>());
    }

    /// <summary>
    /// Live monitoring view for real-time pipeline execution tracking
    /// </summary>
    public IActionResult LiveMonitor(Guid jobId)
    {
        // This view uses SignalR for real-time updates
        return View(jobId);
    }

    /// <summary>
    /// Start pipeline execution
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StartExecution(Guid productId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            var payload = new
            {
                ProductId = productId,
                InitiatedBy = "Web User",
                IsDryRun = false
            };
            
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/PipelineExecution/start", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                return Json(new { 
                    success = true, 
                    executionId = result.GetProperty("pipelineExecutionId").ToString(),
                    jobId = result.GetProperty("hangfireJobId").ToString()
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error = $"Failed to start pipeline: {errorContent}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting pipeline for product {ProductId}", productId);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Pause pipeline execution
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PauseExecution(Guid executionId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.PostAsync($"/api/PipelineExecution/{executionId}/pause", null);
            
            return Json(new { success = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing execution {ExecutionId}", executionId);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Manage SQL scripts for all pipeline stages
    /// </summary>
    public async Task<IActionResult> Scripts(Guid productId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            // Get all pipeline stages
            var stagesResponse = await client.GetAsync("/api/pipelinestages");
            var scriptsResponse = await client.GetAsync($"/api/products/{productId}/scripts");
            var productResponse = await client.GetAsync($"/api/products/{productId}");
            
            if (stagesResponse.IsSuccessStatusCode)
            {
                var stagesContent = await stagesResponse.Content.ReadAsStringAsync();
                var stages = JsonSerializer.Deserialize<List<Contracts.Models.PipelineStage>>(stagesContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                ViewBag.Stages = stages;
                ViewBag.ProductId = productId;
                
                if (productResponse.IsSuccessStatusCode)
                {
                    var productContent = await productResponse.Content.ReadAsStringAsync();
                    var product = JsonSerializer.Deserialize<Contracts.DTOs.ProductDto>(productContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    ViewBag.ProductName = product?.Name;
                }
                
                if (scriptsResponse.IsSuccessStatusCode)
                {
                    var scriptsContent = await scriptsResponse.Content.ReadAsStringAsync();
                    var scripts = JsonSerializer.Deserialize<List<Contracts.Models.PipelineStageScript>>(scriptsContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return View(scripts);
                }
                
                return View(new List<Contracts.Models.PipelineStageScript>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scripts for product {ProductId}", productId);
            ViewBag.Error = "Unable to load scripts. Please try again later.";
        }
        
        return View(new List<Contracts.Models.PipelineStageScript>());
    }

    /// <summary>
    /// Save or update a script
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveScript(Guid productId, Guid stageId, Guid? scriptId, string name, string scriptContent, int executionOrder, bool isActive)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var payload = new
            {
                ProductId = productId,
                PipelineStageId = stageId,
                Name = name,
                ScriptContent = scriptContent,
                ExecutionOrder = executionOrder,
                IsActive = isActive
            };
            
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            HttpResponseMessage response;
            if (scriptId.HasValue && scriptId.Value != Guid.Empty)
            {
                response = await client.PutAsync($"/api/products/{productId}/scripts/{scriptId}", content);
            }
            else
            {
                response = await client.PostAsync($"/api/products/{productId}/scripts", content);
            }
            
            return Json(new { success = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving script");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a script
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteScript(Guid productId, Guid scriptId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.DeleteAsync($"/api/products/{productId}/scripts/{scriptId}");
            
            return Json(new { success = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting script");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// View execution history
    /// </summary>
    public async Task<IActionResult> ExecutionHistory(string? status = null, DateTime? startDate = null, DateTime? endDate = null, string? product = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            
            // Build query parameters
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");
            if (startDate.HasValue) queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(product)) queryParams.Add($"product={Uri.EscapeDataString(product)}");
            
            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/PipelineMonitoring/executions/history{queryString}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var executions = JsonSerializer.Deserialize<List<PipelineExecutionDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return View(executions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading execution history");
            ViewBag.Error = "Unable to load execution history. Please try again later.";
        }
        
        return View(new List<PipelineExecutionDto>());
    }

    /// <summary>
    /// Resume pipeline execution
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ResumeExecution(Guid executionId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.PostAsync($"/api/PipelineExecution/{executionId}/resume", null);
            
            return Json(new { success = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming execution {ExecutionId}", executionId);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel pipeline execution
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CancelExecution(Guid executionId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var payload = new { Reason = "Cancelled by user" };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/PipelineExecution/{executionId}/cancel", content);
            
            return Json(new { success = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling execution {ExecutionId}", executionId);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Save or update a pipeline script
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveScript([FromBody] PipelineStageScriptDto script)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = JsonSerializer.Serialize(script);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            HttpResponseMessage response;
            if (script.Id != Guid.Empty)
            {
                // Update existing
                response = await client.PutAsync($"/api/PipelineScript/{script.Id}", content);
            }
            else
            {
                // Create new
                response = await client.PostAsync("/api/PipelineScript", content);
            }
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var savedScript = JsonSerializer.Deserialize<PipelineStageScriptDto>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return Json(new { success = true, script = savedScript });
            }
            
            return Json(new { success = false, error = "Failed to save script" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving script");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Validate a SQL script
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ValidateScript([FromBody] string sqlScript)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var payload = new { SqlScript = sqlScript };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync("/api/PipelineScript/validate", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ScriptValidationResultDto>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return Json(new { success = true, validation = result });
            }
            
            return Json(new { success = false, error = "Validation failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating script");
            return Json(new { success = false, error = ex.Message });
        }
    }
}
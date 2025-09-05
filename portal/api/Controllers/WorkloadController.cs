using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using API.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Workload Management")]
[RequireRole(Roles.Admin, Roles.ProductAdmin)]
public class WorkloadController : ControllerBase
{
    private readonly IWorkloadDistributionService _workloadDistributionService;
    private readonly IJobDistributionService _jobDistributionService;
    private readonly ILogger<WorkloadController> _logger;

    public WorkloadController(
        IWorkloadDistributionService workloadDistributionService,
        IJobDistributionService jobDistributionService,
        ILogger<WorkloadController> logger)
    {
        _workloadDistributionService = workloadDistributionService;
        _jobDistributionService = jobDistributionService;
        _logger = logger;
    }

    /// <summary>
    /// Distribute pending jobs using specified strategy
    /// </summary>
    [HttpPost("distribute")]
    [SwaggerOperation(Summary = "Distribute Workload", Description = "Distribute pending jobs to agents using specified strategy")]
    [SwaggerResponse(200, "Workload distribution completed", typeof(WorkloadDistributionResult))]
    [SwaggerResponse(400, "Invalid distribution strategy")]
    public async Task<ActionResult<WorkloadDistributionResult>> DistributeWorkload(
        [FromQuery] DistributionStrategy strategy = DistributionStrategy.OptimalPerformance)
    {
        try
        {
            var result = await _workloadDistributionService.DistributeWorkloadAsync(strategy);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute workload using strategy {Strategy}", strategy);
            return StatusCode(500, new { error = "Failed to distribute workload", strategy = strategy.ToString() });
        }
    }

    /// <summary>
    /// Rebalance workload across all online agents
    /// </summary>
    [HttpPost("rebalance")]
    [SwaggerOperation(Summary = "Rebalance Workload", Description = "Rebalance job assignments across online agents")]
    [SwaggerResponse(200, "Workload rebalancing completed", typeof(WorkloadRebalanceResult))]
    public async Task<ActionResult<WorkloadRebalanceResult>> RebalanceWorkload()
    {
        try
        {
            var result = await _workloadDistributionService.RebalanceWorkloadAsync();
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebalance workload");
            return StatusCode(500, new { error = "Failed to rebalance workload" });
        }
    }

    /// <summary>
    /// Get workload distribution recommendations
    /// </summary>
    [HttpGet("recommendations")]
    [SwaggerOperation(Summary = "Get Distribution Recommendations", Description = "Analyze current workload and provide distribution recommendations")]
    [SwaggerResponse(200, "Recommendations retrieved successfully")]
    public async Task<ActionResult> GetDistributionRecommendations()
    {
        try
        {
            var recommendations = await AnalyzeWorkloadAndProvideRecommendationsAsync();
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distribution recommendations");
            return StatusCode(500, new { error = "Failed to get distribution recommendations" });
        }
    }

    /// <summary>
    /// Get current workload statistics
    /// </summary>
    [HttpGet("statistics")]
    [SwaggerOperation(Summary = "Get Workload Statistics", Description = "Get detailed workload statistics across all agents")]
    [SwaggerResponse(200, "Statistics retrieved successfully")]
    public async Task<ActionResult> GetWorkloadStatistics()
    {
        try
        {
            var statistics = await CalculateWorkloadStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workload statistics");
            return StatusCode(500, new { error = "Failed to get workload statistics" });
        }
    }

    /// <summary>
    /// Test different distribution strategies and compare results
    /// </summary>
    [HttpPost("test-strategies")]
    [SwaggerOperation(Summary = "Test Distribution Strategies", Description = "Compare different distribution strategies without actually assigning jobs")]
    [SwaggerResponse(200, "Strategy comparison completed")]
    public async Task<ActionResult> TestDistributionStrategies()
    {
        try
        {
            var results = await CompareDistributionStrategiesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test distribution strategies");
            return StatusCode(500, new { error = "Failed to test distribution strategies" });
        }
    }

    private async Task<object> AnalyzeWorkloadAndProvideRecommendationsAsync()
    {
        // This would analyze current workload patterns and provide recommendations
        // For now, return a basic analysis
        
        var pendingJobs = await _jobDistributionService.DistributePendingJobsAsync();
        
        var recommendations = new
        {
            Timestamp = DateTime.UtcNow,
            AnalysisType = "Basic Workload Analysis",
            PendingJobs = pendingJobs.PendingJobs,
            AvailableAgents = pendingJobs.AvailableAgents,
            Recommendations = new List<object>()
        };

        var recommendationsList = (List<object>)recommendations.Recommendations;

        // Basic recommendation logic
        if (pendingJobs.PendingJobs > 20 && pendingJobs.AvailableAgents < 5)
        {
            recommendationsList.Add(new { 
                Type = "Scale Out", 
                Message = "Consider adding more agents to handle high job volume",
                Priority = "High"
            });
        }

        if (pendingJobs.AvailableAgents > pendingJobs.PendingJobs * 2)
        {
            recommendationsList.Add(new { 
                Type = "Scale Down", 
                Message = "Consider reducing agent capacity during low demand",
                Priority = "Low"
            });
        }

        if (pendingJobs.PendingJobs > 0 && pendingJobs.AvailableAgents > 0)
        {
            recommendationsList.Add(new { 
                Type = "Distribution Strategy", 
                Message = "Use OptimalPerformance strategy for best results",
                Priority = "Medium"
            });
        }

        return recommendations;
    }

    private async Task<object> CalculateWorkloadStatisticsAsync()
    {
        // Calculate detailed workload statistics
        var distributionResult = await _jobDistributionService.DistributePendingJobsAsync();
        
        return new
        {
            Timestamp = DateTime.UtcNow,
            Overview = new
            {
                TotalPendingJobs = distributionResult.PendingJobs,
                TotalAvailableAgents = distributionResult.AvailableAgents,
                JobsAssigned = distributionResult.AssignedJobs,
                FailedAssignments = distributionResult.FailedAssignments,
                DistributionEfficiency = distributionResult.PendingJobs > 0 ? 
                    (distributionResult.AssignedJobs * 100.0 / distributionResult.PendingJobs) : 100
            },
            AgentDistribution = distributionResult.AgentJobCounts.Select(kvp => new
            {
                AgentId = kvp.Key,
                AssignedJobs = kvp.Value
            }).ToList(),
            Messages = distributionResult.Messages
        };
    }

    private async Task<object> CompareDistributionStrategiesAsync()
    {
        // This would simulate different strategies without actually assigning jobs
        // For now, return a conceptual comparison
        
        var strategies = Enum.GetValues<DistributionStrategy>().Select(s => new
        {
            Strategy = s.ToString(),
            Description = GetStrategyDescription(s),
            BestFor = GetStrategyBestUseCase(s),
            Performance = GetStrategyPerformanceRating(s)
        }).ToList();

        return new
        {
            Timestamp = DateTime.UtcNow,
            Strategies = strategies,
            Recommendation = "OptimalPerformance is recommended for most scenarios"
        };
    }

    private static string GetStrategyDescription(DistributionStrategy strategy)
    {
        return strategy switch
        {
            DistributionStrategy.RoundRobin => "Distributes jobs evenly across agents in sequence",
            DistributionStrategy.LoadBalanced => "Assigns jobs to agents with lowest current load",
            DistributionStrategy.PriorityBased => "Matches high-priority jobs to high-priority agents",
            DistributionStrategy.OptimalPerformance => "Uses performance scoring to find best agent for each job",
            DistributionStrategy.Geographic => "Considers agent location for optimal data proximity",
            _ => "Unknown strategy"
        };
    }

    private static string GetStrategyBestUseCase(DistributionStrategy strategy)
    {
        return strategy switch
        {
            DistributionStrategy.RoundRobin => "Simple, predictable distribution",
            DistributionStrategy.LoadBalanced => "Preventing agent overload",
            DistributionStrategy.PriorityBased => "Critical jobs requiring high-performance agents",
            DistributionStrategy.OptimalPerformance => "Maximum overall system efficiency",
            DistributionStrategy.Geographic => "Minimizing data transfer costs",
            _ => "General use"
        };
    }

    private static int GetStrategyPerformanceRating(DistributionStrategy strategy)
    {
        return strategy switch
        {
            DistributionStrategy.RoundRobin => 6,
            DistributionStrategy.LoadBalanced => 8,
            DistributionStrategy.PriorityBased => 7,
            DistributionStrategy.OptimalPerformance => 10,
            DistributionStrategy.Geographic => 9,
            _ => 5
        };
    }
}
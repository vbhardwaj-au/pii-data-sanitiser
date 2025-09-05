using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using DAL.Repositories;
using Contracts.DTOs;
using Contracts.Models;
using API.Authorization;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Pipeline Configuration Management")]
[RequireRole(Roles.Analyst)]
public class PipelineConfigurationController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly IPipelineStageRepository _pipelineStageRepository;
    private readonly IProductPipelineConfigurationRepository _configRepository;
    private readonly IPipelineStageScriptRepository _scriptRepository;
    private readonly ILogger<PipelineConfigurationController> _logger;

    public PipelineConfigurationController(
        IProductRepository productRepository,
        IPipelineStageRepository pipelineStageRepository,
        IProductPipelineConfigurationRepository configRepository,
        IPipelineStageScriptRepository scriptRepository,
        ILogger<PipelineConfigurationController> logger)
    {
        _productRepository = productRepository;
        _pipelineStageRepository = pipelineStageRepository;
        _configRepository = configRepository;
        _scriptRepository = scriptRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get complete pipeline configuration overview for a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <returns>Complete pipeline configuration with stages and scripts</returns>
    [HttpGet("product/{productId}/overview")]
    [SwaggerOperation(Summary = "Get Pipeline Overview", Description = "Gets complete pipeline configuration overview for a product including all stages and scripts")]
    [SwaggerResponse(200, "Pipeline configuration overview", typeof(PipelineConfigurationOverviewDto))]
    [SwaggerResponse(404, "Product not found")]
    public async Task<ActionResult<PipelineConfigurationOverviewDto>> GetPipelineOverview(Guid productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            return NotFound($"Product {productId} not found");
        }

        var stages = (await _pipelineStageRepository.GetAllAsync()).OrderBy(s => s.ExecutionOrder).ToList();
        var configurations = await _configRepository.GetByProductIdAsync(productId);
        var allScripts = await _scriptRepository.GetByProductIdAsync(productId);

        var configDict = configurations.ToDictionary(c => c.PipelineStageId, c => c);

        var stageConfigurations = new List<PipelineStageConfigurationDto>();

        foreach (var stage in stages)
        {
            var hasConfiguration = configDict.TryGetValue(stage.Id, out var config);
            var stageScripts = allScripts.Where(s => s.PipelineStageId == stage.Id).ToList();

            stageConfigurations.Add(new PipelineStageConfigurationDto
            {
                Stage = new PipelineStageDto
                {
                    Id = stage.Id,
                    Name = stage.Name,
                    DisplayName = stage.DisplayName,
                    Description = stage.Description,
                    ExecutionOrder = stage.ExecutionOrder,
                    IsRequired = stage.IsRequired,
                    DefaultTimeoutMinutes = stage.DefaultTimeoutMinutes,
                    IsActive = stage.IsActive,
                    CreatedAt = stage.CreatedAt,
                    UpdatedAt = stage.UpdatedAt
                },
                Configuration = hasConfiguration ? new ProductPipelineConfigurationDto
                {
                    Id = config!.Id,
                    ProductId = config.ProductId,
                    PipelineStageId = config.PipelineStageId,
                    IsEnabled = config.IsEnabled,
                    Configuration = config.Configuration,
                    TimeoutMinutes = config.TimeoutMinutes,
                    ParallelExecutionEnabled = config.ParallelExecutionEnabled,
                    OnFailureAction = config.OnFailureAction,
                    RetryAttempts = config.RetryAttempts,
                    RetryDelaySeconds = config.RetryDelaySeconds,
                    Notes = config.Notes,
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt,
                    CreatedBy = config.CreatedBy,
                    UpdatedBy = config.UpdatedBy
                } : null,
                Scripts = stageScripts.Select(s => new PipelineStageScriptDto
                {
                    Id = s.Id,
                    ProductId = s.ProductId,
                    PipelineStageId = s.PipelineStageId,
                    Name = s.Name,
                    Description = s.Description,
                    SqlScript = s.SqlScript,
                    ExecutionOrder = s.ExecutionOrder,
                    IsEnabled = s.IsEnabled,
                    TimeoutMinutes = s.TimeoutMinutes,
                    OnFailureAction = s.OnFailureAction,
                    RetryAttempts = s.RetryAttempts,
                    RetryDelaySeconds = s.RetryDelaySeconds,
                    IsSecurityValidated = s.IsSecurityValidated,
                    SecurityValidationNotes = s.SecurityValidationNotes,
                    LastValidatedAt = s.LastValidatedAt,
                    ExpectedResultType = s.ExpectedResultType,
                    ExpectedMinRows = s.ExpectedMinRows,
                    ExpectedMaxRows = s.ExpectedMaxRows,
                    Tags = s.Tags,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    CreatedBy = s.CreatedBy,
                    UpdatedBy = s.UpdatedBy
                }).OrderBy(s => s.ExecutionOrder).ToList(),
                IsEnabled = config?.IsEnabled ?? false,
                ExecutionOrder = stage.ExecutionOrder
            });
        }

        var totalExecutions = 0; // TODO: Get from execution repository
        var successfulExecutions = 0; // TODO: Get from execution repository

        var overview = new PipelineConfigurationOverviewDto
        {
            Product = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                // DatabaseName = product.DatabaseName,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            },
            Stages = stageConfigurations,
            Settings = new PipelineSettingsDto
            {
                EstimatedDurationMinutes = 30,
                MaxRetryCount = 3,
                ContinueOnFailure = false,
                ParallelExecutionEnabled = false,
                MaxConcurrentStages = 1
            },
            Statistics = new PipelineStatisticsDto
            {
                TotalExecutions = totalExecutions,
                SuccessfulExecutions = successfulExecutions,
                FailedExecutions = totalExecutions - successfulExecutions,
                AverageExecutionMinutes = 25.5,
                LastExecutionDate = DateTime.UtcNow.AddDays(-1)
            }
        };

        return Ok(overview);
    }

    /// <summary>
    /// Get all products with their pipeline configuration status
    /// </summary>
    /// <returns>List of products with configuration status</returns>
    [HttpGet("products")]
    [SwaggerOperation(Summary = "Get Products Configuration Status", Description = "Gets all products with their pipeline configuration status")]
    [SwaggerResponse(200, "Products with configuration status", typeof(List<ProductConfigurationStatusDto>))]
    public async Task<ActionResult<List<ProductConfigurationStatusDto>>> GetProductsConfigurationStatus()
    {
        var products = await _productRepository.GetAllAsync();
        var result = new List<ProductConfigurationStatusDto>();

        foreach (var product in products)
        {
            var configurations = await _configRepository.GetByProductIdAsync(product.Id);
            var scripts = await _scriptRepository.GetByProductIdAsync(product.Id);

            result.Add(new ProductConfigurationStatusDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ConfiguredStages = configurations.Count(),
                EnabledStages = configurations.Count(c => c.IsEnabled),
                TotalScripts = scripts.Count(),
                EnabledScripts = scripts.Count(s => s.IsEnabled),
                LastConfigurationUpdate = configurations.Any() ? configurations.Max(c => c.UpdatedAt) : null
            });
        }

        return Ok(result);
    }
}

/// <summary>
/// Product configuration status summary
/// </summary>
public class ProductConfigurationStatusDto
{
    /// <summary>
    /// Product identifier
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Product name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Number of configured stages
    /// </summary>
    public int ConfiguredStages { get; set; }

    /// <summary>
    /// Number of enabled stages
    /// </summary>
    public int EnabledStages { get; set; }

    /// <summary>
    /// Total number of scripts
    /// </summary>
    public int TotalScripts { get; set; }

    /// <summary>
    /// Number of enabled scripts
    /// </summary>
    public int EnabledScripts { get; set; }

    /// <summary>
    /// Last configuration update timestamp
    /// </summary>
    public DateTime? LastConfigurationUpdate { get; set; }
}
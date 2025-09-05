using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using DAL.Repositories;
using API.Services;
using Contracts.Models;
using API.Authorization;
using Contracts.DTOs;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Pipeline Script Management")]
[RequireRole(Roles.ProductAdmin)]
public class PipelineScriptController : ControllerBase
{
    private readonly IPipelineStageScriptRepository _scriptRepository;
    private readonly IProductPipelineConfigurationRepository _configRepository;
    private readonly IScriptExecutionLogRepository _logRepository;
    private readonly ISqlScriptValidatorService _validatorService;
    private readonly ILogger<PipelineScriptController> _logger;

    public PipelineScriptController(
        IPipelineStageScriptRepository scriptRepository,
        IProductPipelineConfigurationRepository configRepository,
        IScriptExecutionLogRepository logRepository,
        ISqlScriptValidatorService validatorService,
        ILogger<PipelineScriptController> logger)
    {
        _scriptRepository = scriptRepository;
        _configRepository = configRepository;
        _logRepository = logRepository;
        _validatorService = validatorService;
        _logger = logger;
    }

    /// <summary>
    /// Get all scripts for a specific product and stage
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="stageId">Pipeline stage ID</param>
    /// <returns>List of scripts for the specified product and stage</returns>
    [HttpGet("product/{productId}/stage/{stageId}")]
    [SwaggerOperation(Summary = "Get Scripts by Product and Stage", Description = "Gets all scripts for a specific product and pipeline stage")]
    [SwaggerResponse(200, "Scripts retrieved successfully", typeof(List<PipelineStageScriptDto>))]
    [SwaggerResponse(404, "Product or stage not found")]
    public async Task<ActionResult<List<PipelineStageScriptDto>>> GetScriptsByProductAndStage(Guid productId, Guid stageId)
    {
        var scripts = await _scriptRepository.GetByProductAndStageAsync(productId, stageId);
        
        var scriptDtos = scripts.Select(s => new PipelineStageScriptDto
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
        }).OrderBy(s => s.ExecutionOrder).ToList();

        return Ok(scriptDtos);
    }

    /// <summary>
    /// Create a new script for a pipeline stage
    /// </summary>
    /// <param name="request">Script creation request</param>
    /// <returns>Created script details</returns>
    [HttpPost]
    [SwaggerOperation(Summary = "Create Script", Description = "Creates a new script for a pipeline stage")]
    [SwaggerResponse(201, "Script created successfully", typeof(PipelineStageScriptDto))]
    [SwaggerResponse(400, "Invalid request data")]
    [SwaggerResponse(409, "Script with same name already exists")]
    public async Task<ActionResult<PipelineStageScriptDto>> CreateScript(CreateScriptRequest request)
    {
        // Check if script with same name exists
        var exists = await _scriptRepository.ExistsAsync(request.ProductId, request.PipelineStageId, request.Name);
        if (exists)
        {
            return Conflict($"Script with name '{request.Name}' already exists for this product and stage");
        }

        var script = new PipelineStageScript
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            PipelineStageId = request.PipelineStageId,
            Name = request.Name,
            Description = request.Description,
            SqlScript = request.SqlScript,
            ExecutionOrder = await _scriptRepository.GetNextExecutionOrderAsync(request.ProductId, request.PipelineStageId),
            IsEnabled = request.IsEnabled,
            TimeoutMinutes = request.TimeoutMinutes,
            OnFailureAction = request.OnFailureAction,
            RetryAttempts = request.RetryAttempts,
            RetryDelaySeconds = request.RetryDelaySeconds,
            ExpectedResultType = request.ExpectedResultType,
            ExpectedMinRows = request.ExpectedMinRows,
            ExpectedMaxRows = request.ExpectedMaxRows,
            Tags = request.Tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "System", // TODO: Get from current user context
            UpdatedBy = "System"
        };

        var created = await _scriptRepository.CreateAsync(script);

        var scriptDto = new PipelineStageScriptDto
        {
            Id = created.Id,
            ProductId = created.ProductId,
            PipelineStageId = created.PipelineStageId,
            Name = created.Name,
            Description = created.Description,
            SqlScript = created.SqlScript,
            ExecutionOrder = created.ExecutionOrder,
            IsEnabled = created.IsEnabled,
            TimeoutMinutes = created.TimeoutMinutes,
            OnFailureAction = created.OnFailureAction,
            RetryAttempts = created.RetryAttempts,
            RetryDelaySeconds = created.RetryDelaySeconds,
            IsSecurityValidated = created.IsSecurityValidated,
            SecurityValidationNotes = created.SecurityValidationNotes,
            LastValidatedAt = created.LastValidatedAt,
            ExpectedResultType = created.ExpectedResultType,
            ExpectedMinRows = created.ExpectedMinRows,
            ExpectedMaxRows = created.ExpectedMaxRows,
            Tags = created.Tags,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
            CreatedBy = created.CreatedBy,
            UpdatedBy = created.UpdatedBy
        };

        return CreatedAtAction(nameof(GetScriptsByProductAndStage), 
            new { productId = created.ProductId, stageId = created.PipelineStageId }, 
            scriptDto);
    }

    /// <summary>
    /// Validate a SQL script for security and syntax
    /// </summary>
    /// <param name="request">Script validation request</param>
    /// <returns>Validation results</returns>
    [HttpPost("validate")]
    [SwaggerOperation(Summary = "Validate Script", Description = "Validates a SQL script for security and syntax issues")]
    [SwaggerResponse(200, "Validation completed", typeof(ScriptValidationResultDto))]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<ActionResult<ScriptValidationResultDto>> ValidateScript(ValidateScriptRequest request)
    {
        try
        {
            var result = await _validatorService.ValidateScriptAsync(request.SqlScript);
            
            return Ok(new ScriptValidationResultDto
            {
                IsValid = result.IsValid,
                Warnings = result.Warnings?.ToList() ?? new List<string>(),
                // Errors = result.Errors?.ToList() ?? new List<string>(),
                SecurityIssues = result.SecurityIssues?.Select(si => new SecurityIssueDto { Description = si.Description, Severity = si.Severity }).ToList() ?? new List<SecurityIssueDto>(),
                // EstimatedExecutionTimeSeconds = result.EstimatedExecutionTimeSeconds,
                ValidatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating script");
            return BadRequest($"Validation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Request model for creating a new script
/// </summary>
public class CreateScriptRequest
{
    /// <summary>
    /// Product ID this script belongs to
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Pipeline stage ID this script belongs to
    /// </summary>
    public Guid PipelineStageId { get; set; }

    /// <summary>
    /// Script name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Script description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// SQL script content
    /// </summary>
    public string SqlScript { get; set; } = string.Empty;

    /// <summary>
    /// Whether this script is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Script timeout in minutes
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Action to take on failure (Stop, Skip, Warn)
    /// </summary>
    public string OnFailureAction { get; set; } = "Stop";

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryAttempts { get; set; } = 0;

    /// <summary>
    /// Delay between retries in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Expected result type
    /// </summary>
    public string ExpectedResultType { get; set; } = "None";

    /// <summary>
    /// Expected minimum rows
    /// </summary>
    public long? ExpectedMinRows { get; set; }

    /// <summary>
    /// Expected maximum rows
    /// </summary>
    public long? ExpectedMaxRows { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// Request model for script validation
/// </summary>
public class ValidateScriptRequest
{
    /// <summary>
    /// SQL script to validate
    /// </summary>
    public string SqlScript { get; set; } = string.Empty;
}
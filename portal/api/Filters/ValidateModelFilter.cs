using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;
using Contracts.Models;

namespace API.Filters;

public class ValidateModelFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Title = "Validation Failed",
                Detail = "One or more validation errors occurred",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            };

            // Add correlation ID if available
            if (context.HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId.ToString();
            }

            problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
            problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            context.Result = new BadRequestObjectResult(problemDetails);
        }
    }
}

// Custom validation attributes for pipeline operations
public class ValidPipelineStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string status)
            return false;

        return PipelineExecutionStatus.IsValid(status);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be a valid pipeline status. Valid values are: {string.Join(", ", PipelineExecutionStatus.All)}";
    }
}

public class ValidStageStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string status)
            return false;

        return StageExecutionStatus.IsValid(status);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be a valid stage status. Valid values are: {string.Join(", ", StageExecutionStatus.All)}";
    }
}

// Pipeline-specific validation attributes
public class RequiredGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is Guid guid)
        {
            return guid != Guid.Empty;
        }
        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field is required and must be a valid non-empty GUID";
    }
}
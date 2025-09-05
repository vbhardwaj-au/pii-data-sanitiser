using System.Net;
using System.Text.Json;

namespace API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var problemDetails = exception switch
        {
            ArgumentNullException nullEx => new ProblemDetails
            {
                Title = "Required Parameter Missing",
                Detail = $"Required parameter '{nullEx.ParamName}' was null or missing",
                Status = (int)HttpStatusCode.BadRequest,
                Instance = context.Request.Path
            },
            ArgumentException argEx => new ProblemDetails
            {
                Title = "Invalid Argument",
                Detail = argEx.Message,
                Status = (int)HttpStatusCode.BadRequest,
                Instance = context.Request.Path
            },
            InvalidOperationException invalidOpEx => new ProblemDetails
            {
                Title = "Invalid Operation",
                Detail = invalidOpEx.Message,
                Status = (int)HttpStatusCode.BadRequest,
                Instance = context.Request.Path
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Access to this resource is unauthorized",
                Status = (int)HttpStatusCode.Unauthorized,
                Instance = context.Request.Path
            },
            KeyNotFoundException notFoundEx => new ProblemDetails
            {
                Title = "Resource Not Found",
                Detail = notFoundEx.Message,
                Status = (int)HttpStatusCode.NotFound,
                Instance = context.Request.Path
            },
            TimeoutException timeoutEx => new ProblemDetails
            {
                Title = "Request Timeout",
                Detail = timeoutEx.Message,
                Status = (int)HttpStatusCode.RequestTimeout,
                Instance = context.Request.Path
            },
            NotSupportedException notSupportedEx => new ProblemDetails
            {
                Title = "Operation Not Supported",
                Detail = notSupportedEx.Message,
                Status = (int)HttpStatusCode.NotImplemented,
                Instance = context.Request.Path
            },
            _ => new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing the request",
                Status = (int)HttpStatusCode.InternalServerError,
                Instance = context.Request.Path
            }
        };

        // Add correlation ID if available
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId.ToString();
        }

        // Add timestamp
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        // Add trace ID for debugging
        if (context.TraceIdentifier != null)
        {
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        }

        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}

public class ProblemDetails
{
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public int? Status { get; set; }
    public string? Instance { get; set; }
    public string Type { get; set; } = "about:blank";
    public Dictionary<string, object> Extensions { get; set; } = new();
}

// Extension method for easy registration
public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
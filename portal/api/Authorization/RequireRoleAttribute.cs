using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredRoles;

    public RequireRoleAttribute(params string[] requiredRoles)
    {
        _requiredRoles = requiredRoles ?? throw new ArgumentNullException(nameof(requiredRoles));
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Check if user is authenticated
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Unauthorized",
                message = "Authentication required",
                timestamp = DateTime.UtcNow
            });
            return;
        }

        // Check if user has any of the required roles
        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var hasRequiredRole = _requiredRoles.Any(role => 
            userRoles.Contains(role, StringComparer.OrdinalIgnoreCase));

        if (!hasRequiredRole)
        {
            var userName = user.Identity.Name ?? "Unknown";
            var userRolesList = string.Join(", ", userRoles);
            var requiredRolesList = string.Join(", ", _requiredRoles);

            context.Result = new ForbiddenObjectResult(new
            {
                error = "Forbidden",
                message = $"Access denied. Required roles: {requiredRolesList}. User roles: {userRolesList}",
                user = userName,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

// Custom result for forbidden access
public class ForbiddenObjectResult : ObjectResult
{
    public ForbiddenObjectResult(object value) : base(value)
    {
        StatusCode = 403;
    }
}

// Common role constants
public static class Roles
{
    public const string Admin = "Admin";
    public const string ProductAdmin = "ProductAdmin";
    public const string Analyst = "Analyst";
    public const string Viewer = "Viewer";
    public const string User = "User";
    
    // Composite roles for convenience
    public static readonly string[] AdminRoles = { Admin };
    public static readonly string[] ProductAdminRoles = { Admin, ProductAdmin };
    public static readonly string[] AnalystRoles = { Admin, ProductAdmin, Analyst };
    public static readonly string[] UserRoles = { Admin, ProductAdmin, Analyst, User };
    public static readonly string[] ViewerRoles = { Admin, ProductAdmin, Analyst, User, Viewer };
}

// Extension methods for easier authorization checks
public static class ClaimsPrincipalExtensions
{
    public static bool HasRole(this ClaimsPrincipal user, string role)
    {
        return user.IsInRole(role);
    }

    public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
    {
        return roles.Any(role => user.IsInRole(role));
    }

    public static string? GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public static string? GetUserName(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value;
    }

    public static string? GetApiKeyId(this ClaimsPrincipal user)
    {
        return user.FindFirst("ApiKeyId")?.Value;
    }

    public static List<string> GetUserRoles(this ClaimsPrincipal user)
    {
        return user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }
}
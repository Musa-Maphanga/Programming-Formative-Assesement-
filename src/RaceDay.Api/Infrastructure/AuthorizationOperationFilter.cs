using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Infrastructure;

/// <summary>
/// Makes Swagger show who can call each endpoint. For endpoints that need a login it:
/// adds the padlock (Bearer token), adds a "Role required" line to the description,
/// and lists the 401 and 403 responses so they appear next to the success response.
/// </summary>
public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var classAttrs = method.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>();
        var methodAttrs = method.GetCustomAttributes(true);

        bool allowAnonymous = methodAttrs.OfType<AllowAnonymousAttribute>().Any()
                              || (classAttrs.OfType<AllowAnonymousAttribute>().Any()
                                  && !methodAttrs.OfType<AuthorizeAttribute>().Any());

        var authorize = methodAttrs.OfType<AuthorizeAttribute>()
            .Concat(classAttrs.OfType<AuthorizeAttribute>())
            .ToList();

        if (allowAnonymous || authorize.Count == 0)
        {
            operation.Description = AppendLine(operation.Description, "**Role required:** None (public)");
            return;
        }

        var roles = authorize
            .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
            .SelectMany(a => a.Roles!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();

        var roleText = roles.Count == 0 ? "Any (logged in)" : string.Join(" or ", roles);
        operation.Description = AppendLine(operation.Description, $"**Role required:** {roleText}");

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized – no token, or the token is invalid or expired." });
        if (roles.Count > 0)
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden – the logged-in user does not have the required role." });

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            }
        };
    }

    private static string AppendLine(string? existing, string line) =>
        string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n\n{line}";
}

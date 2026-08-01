using ServiceConfiguration;

namespace QueryService.Api.Middleware;

public class StaticTokenMiddleware
{
    private readonly RequestDelegate _next;

    public StaticTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var config = context.RequestServices.GetRequiredService<ServiceBootstrap>();
        var headerName = config.Auth.HeaderName ?? "X-Api-Token";

        if (!context.Request.Headers.TryGetValue(headerName, out var providedToken) ||
            providedToken != config.Auth.IssuedToken)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"code":"UNAUTHORIZED","message":"Valid API token is required"}""");
            return;
        }

        await _next(context);
    }
}

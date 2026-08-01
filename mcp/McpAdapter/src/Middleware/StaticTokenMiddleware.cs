using ServiceConfiguration;

namespace McpAdapter.Middleware;

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

        // Try the configured header first, then fall back to the ?token= query parameter.
        // The query-parameter form enables tokenized connector URLs for ChatGPT dev mode.
        var providedToken = context.Request.Headers[headerName].FirstOrDefault()
            ?? context.Request.Query["token"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedToken) || providedToken != config.Auth.IssuedToken)
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

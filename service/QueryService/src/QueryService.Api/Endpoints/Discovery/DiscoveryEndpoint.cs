using Microsoft.AspNetCore.Mvc;
using QueryService.Domain.Services;
using ServiceContract.V1;

namespace QueryService.Api.Endpoints.Discovery;

public static class DiscoveryEndpoint
{
    public const string ResourceName = "discovery";
    public const int Version = 1;

    public static class Registration
    {
        public static RouteHandlerBuilder Map(RouteGroupBuilder group)
        {
            return group.MapPost("/discovery", Handler.HandleAsync)
                .WithName("Discovery")
                .WithTags("Discovery")
                .Produces<DiscoveryResponse>(StatusCodes.Status200OK)
                .Produces<QueryError>(StatusCodes.Status400BadRequest)
                .Produces<QueryError>(StatusCodes.Status401Unauthorized)
                .Produces<QueryError>(StatusCodes.Status500InternalServerError);
        }
    }

    public static class Handler
    {
        public static async Task<IResult> HandleAsync(
            [FromBody] DiscoveryRequest request,
            IDiscoveryService discoveryService,
            CancellationToken cancellationToken)
        {
            var response = await discoveryService.DiscoverAsync(request, cancellationToken);
            return Results.Ok(response);
        }
    }
}

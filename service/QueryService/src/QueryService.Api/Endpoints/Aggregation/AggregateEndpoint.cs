using Microsoft.AspNetCore.Mvc;
using QueryService.Domain.Services;
using ServiceContract.V1;

namespace QueryService.Api.Endpoints.Aggregation;

public static class AggregateEndpoint
{
    public const string ResourceName = "aggregation";
    public const int Version = 1;

    public static class Registration
    {
        public static RouteHandlerBuilder Map(RouteGroupBuilder group)
        {
            return group.MapPost("/aggregation", Handler.HandleAsync)
                .WithName("Aggregate")
                .WithTags("Aggregation")
                .Produces<AggregateResponse>(StatusCodes.Status200OK)
                .Produces<QueryError>(StatusCodes.Status400BadRequest)
                .Produces<QueryError>(StatusCodes.Status401Unauthorized)
                .Produces<QueryError>(StatusCodes.Status500InternalServerError);
        }
    }

    public static class Handler
    {
        public static async Task<IResult> HandleAsync(
            [FromBody] AggregateRequest request,
            IAggregateService aggregateService,
            CancellationToken cancellationToken)
        {
            var response = await aggregateService.AggregateAsync(request, cancellationToken);
            return Results.Ok(response);
        }
    }
}

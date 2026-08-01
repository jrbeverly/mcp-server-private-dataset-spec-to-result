using Microsoft.AspNetCore.Mvc;
using QueryService.Domain.Services;
using ServiceContract.V1;

namespace QueryService.Api.Endpoints.DrillDown;

public static class DrillDownEndpoint
{
    public const string ResourceName = "drill-down";
    public const int Version = 1;

    public static class Registration
    {
        public static RouteHandlerBuilder Map(RouteGroupBuilder group)
        {
            return group.MapPost("/drill-down", Handler.HandleAsync)
                .WithName("DrillDown")
                .WithTags("DrillDown")
                .Produces<DrillDownResponse>(StatusCodes.Status200OK)
                .Produces<QueryError>(StatusCodes.Status404NotFound)
                .Produces<QueryError>(StatusCodes.Status401Unauthorized)
                .Produces<QueryError>(StatusCodes.Status500InternalServerError);
        }
    }

    public static class Handler
    {
        public static async Task<IResult> HandleAsync(
            [FromBody] DrillDownRequest request,
            IDrillDownService drillDownService,
            CancellationToken cancellationToken)
        {
            var response = await drillDownService.DrillDownAsync(request, cancellationToken);
            return Results.Ok(response);
        }
    }
}

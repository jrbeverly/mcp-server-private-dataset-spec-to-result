using Microsoft.AspNetCore.Mvc;
using QueryService.Domain.Services;
using ServiceContract.V1;

namespace QueryService.Api.Endpoints.Metadata;

public static class MetadataExplorationEndpoint
{
    public const string ResourceName = "metadata-exploration";
    public const int Version = 1;

    public static class Registration
    {
        public static RouteHandlerBuilder Map(RouteGroupBuilder group)
        {
            return group.MapPost("/metadata", Handler.HandleAsync)
                .WithName("ExploreMetadata")
                .WithTags("Metadata")
                .Produces<MetadataExplorationResponse>(StatusCodes.Status200OK)
                .Produces<QueryError>(StatusCodes.Status400BadRequest)
                .Produces<QueryError>(StatusCodes.Status401Unauthorized)
                .Produces<QueryError>(StatusCodes.Status500InternalServerError);
        }
    }

    public static class Handler
    {
        public static async Task<IResult> HandleAsync(
            [FromBody] MetadataExplorationRequest request,
            IMetadataExplorationService metadataService,
            CancellationToken cancellationToken)
        {
            var response = await metadataService.ExploreMetadataAsync(request, cancellationToken);
            return Results.Ok(response);
        }
    }
}

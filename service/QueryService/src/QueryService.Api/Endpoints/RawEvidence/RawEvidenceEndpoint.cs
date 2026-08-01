using Microsoft.AspNetCore.Mvc;
using QueryService.Domain.Services;
using ServiceContract.V1;

namespace QueryService.Api.Endpoints.RawEvidence;

public static class RawEvidenceEndpoint
{
    public const string ResourceName = "raw-evidence";
    public const int Version = 1;

    public static class Registration
    {
        public static RouteHandlerBuilder Map(RouteGroupBuilder group)
        {
            return group.MapPost("/raw-evidence", Handler.HandleAsync)
                .WithName("GetRawEvidence")
                .WithTags("RawEvidence")
                .Produces<RawEvidenceResponse>(StatusCodes.Status200OK)
                .Produces<QueryError>(StatusCodes.Status404NotFound)
                .Produces<QueryError>(StatusCodes.Status401Unauthorized)
                .Produces<QueryError>(StatusCodes.Status500InternalServerError);
        }
    }

    public static class Handler
    {
        public static async Task<IResult> HandleAsync(
            [FromBody] RawEvidenceRequest request,
            IRawEvidenceService evidenceService,
            CancellationToken cancellationToken)
        {
            var response = await evidenceService.GetRawEvidenceAsync(request, cancellationToken);
            return Results.Ok(response);
        }
    }
}

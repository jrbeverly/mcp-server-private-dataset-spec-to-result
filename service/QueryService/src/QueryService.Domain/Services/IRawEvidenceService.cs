using ServiceContract.V1;

namespace QueryService.Domain.Services;

public interface IRawEvidenceService
{
    Task<RawEvidenceResponse> GetRawEvidenceAsync(
        RawEvidenceRequest request,
        CancellationToken cancellationToken = default);
}

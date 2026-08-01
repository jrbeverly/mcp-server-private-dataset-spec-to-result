using ServiceContract.V1;

namespace QueryService.Domain.Services;

public interface IDrillDownService
{
    Task<DrillDownResponse> DrillDownAsync(DrillDownRequest request, CancellationToken cancellationToken = default);
}

using ServiceContract.V1;

namespace QueryService.Domain.Services;

public interface IAggregateService
{
    Task<AggregateResponse> AggregateAsync(AggregateRequest request, CancellationToken cancellationToken = default);
}

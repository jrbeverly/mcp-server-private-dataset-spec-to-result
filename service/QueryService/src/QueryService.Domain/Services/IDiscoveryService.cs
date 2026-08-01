using ServiceContract.V1;

namespace QueryService.Domain.Services;

public interface IDiscoveryService
{
    Task<DiscoveryResponse> DiscoverAsync(DiscoveryRequest request, CancellationToken cancellationToken = default);
}

using EvaluationHarness.Models;

namespace EvaluationHarness.Storage;

public interface IResultStore
{
    Task SaveAsync(BenchmarkRunResult result, CancellationToken ct = default);
    Task<BenchmarkRunResult?> LoadAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListRunIdsAsync(CancellationToken ct = default);
}

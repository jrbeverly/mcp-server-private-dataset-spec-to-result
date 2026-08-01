using System.Text.Json;
using EvaluationHarness.Models;

namespace EvaluationHarness.Storage;

public sealed class FileSystemResultStore : IResultStore
{
    private readonly string _directory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileSystemResultStore(string directory)
    {
        _directory = directory;
    }

    public async Task SaveAsync(BenchmarkRunResult result, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_directory);

        var path = FilePath(result.RunId);
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public Task<BenchmarkRunResult?> LoadAsync(string runId, CancellationToken ct = default)
    {
        var path = FilePath(runId);
        if (!File.Exists(path))
            return Task.FromResult<BenchmarkRunResult?>(null);

        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<BenchmarkRunResult>(json, JsonOptions);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<string>> ListRunIdsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_directory);

        var ids = Directory.GetFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Cast<string>()
            .OrderDescending()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(ids);
    }

    private string FilePath(string runId) => Path.Combine(_directory, $"{runId}.json");
}

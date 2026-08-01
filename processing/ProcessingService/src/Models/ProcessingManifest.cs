using System.Security.Cryptography;
using System.Text;

namespace ProcessingService.Models;

public sealed record ProcessingManifest
{
    public required string VersionId { get; init; }
    public required string SourceRawArtifactPath { get; init; }
    public required string PipelineVersion { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
    public required int EntityCount { get; init; }
    public string? RawInputSha256 { get; init; }

    public static string ComputeInputHash(string rawArtifactPath)
    {
        if (Directory.Exists(rawArtifactPath))
        {
            using var sha = SHA256.Create();
            var files = Directory.GetFiles(rawArtifactPath, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal);

            foreach (var file in files)
            {
                var bytes = File.ReadAllBytes(file);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        if (File.Exists(rawArtifactPath))
        {
            var bytes = File.ReadAllBytes(rawArtifactPath);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        throw new InvalidOperationException($"Raw artifact path '{rawArtifactPath}' does not exist");
    }
}

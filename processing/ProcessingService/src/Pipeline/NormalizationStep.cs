using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ProcessingService.Models;

namespace ProcessingService.Pipeline;

public static class NormalizationStep
{
    public static async Task<IReadOnlyList<CanonicalEntity>> NormalizeAsync(
        string rawArtifactPath,
        CancellationToken cancellationToken = default)
    {
        var csvFiles = FindCsvFiles(rawArtifactPath);
        var entities = new List<CanonicalEntity>();

        foreach (var csvFile in csvFiles)
        {
            var fileEntities = await ParseCsvFileAsync(csvFile, cancellationToken);
            entities.AddRange(fileEntities);
        }

        return entities;
    }

    private static string[] FindCsvFiles(string path)
    {
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path, "*.csv", SearchOption.AllDirectories);
            var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
            var allFiles = files.Concat(jsonFiles).ToArray();

            if (allFiles.Length == 0)
                throw new InvalidOperationException(
                    $"No CSV or JSON files found in directory '{path}'.");

            return allFiles;
        }

        if (File.Exists(path) && Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return new[] { path };

        if (File.Exists(path) && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return new[] { path };

        throw new InvalidOperationException(
            $"No CSV or JSON files found at '{path}'. Provide a CSV file, a JSON array file, or a directory containing them.");
    }

    private static async Task<IReadOnlyList<CanonicalEntity>> ParseCsvFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return ParseJsonFile(filePath);

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        var entities = new List<CanonicalEntity>();
        while (await csv.ReadAsync())
        {
            var entity = MapRowToEntity(csv, headers);
            entities.Add(entity);
        }

        return entities;
    }

    private static IReadOnlyList<CanonicalEntity> ParseJsonFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var entities = System.Text.Json.JsonSerializer.Deserialize<List<JsonEntity>>(json)
            ?? new List<JsonEntity>();

        return entities.Select(e => new CanonicalEntity
        {
            EntityId = e.EntityId ?? e.Id ?? Guid.NewGuid().ToString(),
            Name = e.Name ?? e.EntityName ?? "Unnamed",
            Category = e.Category,
            Description = e.Description,
            Properties = e.Properties ?? new Dictionary<string, string>(),
            Metrics = e.Metrics ?? new Dictionary<string, decimal>()
        }).ToList();
    }

    private sealed record JsonEntity
    {
        public string? EntityId { get; init; }
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? EntityName { get; init; }
        public string? Category { get; init; }
        public string? Description { get; init; }
        public Dictionary<string, string>? Properties { get; init; }
        public Dictionary<string, decimal>? Metrics { get; init; }
    }

    private static CanonicalEntity MapRowToEntity(CsvReader csv, string[] headers)
    {
        var entityId = GetField(csv, headers, "entity_id", "id", "entityid");
        var name = GetField(csv, headers, "name", "entity_name", "entityname", "title");
        var category = GetField(csv, headers, "category");
        var description = GetField(csv, headers, "description", "desc");

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var knownColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "entity_id", "id", "entityid", "name", "entity_name", "entityname",
            "title", "category", "description", "desc"
        };

        foreach (var header in headers)
        {
            if (knownColumns.Contains(header)) continue;

            var value = csv.GetField(header);
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                metrics[header] = num;
            else
                properties[header] = value;
        }

        return new CanonicalEntity
        {
            EntityId = !string.IsNullOrWhiteSpace(entityId) ? entityId : Guid.NewGuid().ToString(),
            Name = !string.IsNullOrWhiteSpace(name) ? name : "Unnamed",
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Properties = properties,
            Metrics = metrics
        };
    }

    private static string? GetField(CsvReader csv, string[] headers, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            foreach (var header in headers)
            {
                if (string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase))
                    return csv.GetField(header);
            }
        }
        return null;
    }
}

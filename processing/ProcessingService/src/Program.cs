using System.Text.Json;
using IngestionService.Services;
using ProcessingService.Pipeline;
using ProcessingService.Services;
using ServiceContract.VersionRegistry;

try
{
    var connectionString = Environment.GetEnvironmentVariable("STORAGE_POSTGRES_CONNECTION_STRING")
        ?? "Host=localhost;Database=corpus;Username=postgres;Password=postgres";
    var artifactRoot = Environment.GetEnvironmentVariable("ARTIFACT_ROOT_PATH")
        ?? Path.Combine(Path.GetTempPath(), "processing-artifacts");

    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    var cmdIndex = Array.FindIndex(args, a => a is "process" or "publish" or "list" or "show" or "init-db");

    if (cmdIndex < 0)
    {
        Console.Error.WriteLine($"Unknown command. Use 'ProcessingService' or 'dotnet run' with a command.");
        PrintUsage();
        return 1;
    }

    var command = args[cmdIndex];
    var cmdArgs = args.Skip(cmdIndex + 1).ToArray();

    await using var registry = new PostgresVersionRegistry(connectionString);
    var artifactStore = new ProcessedArtifactStore(artifactRoot);
    var pipeline = new ProcessingPipeline(artifactStore, registry);

    switch (command)
    {
        case "process":
            await HandleProcess(pipeline, cmdArgs);
            break;
        case "publish":
            await HandlePublish(pipeline, cmdArgs);
            break;
        case "list":
            await HandleList(registry);
            break;
        case "show":
            await HandleShow(registry, cmdArgs);
            break;
        case "init-db":
            await HandleInitDb(registry);
            break;
        default:
            PrintUsage();
            return 1;
    }

    return 0;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"ERROR: {ex.Message}");
    return 1;
}

static async Task HandleProcess(ProcessingPipeline pipeline, string[] args)
{
    var versionId = GetArgValue(args, "--version-id");
    if (string.IsNullOrEmpty(versionId))
    {
        await Console.Error.WriteLineAsync("ERROR: --version-id is required for process");
        return;
    }

    Console.WriteLine($"Processing version '{versionId}'...");
    var output = await pipeline.ProcessAsync(versionId);

    var summary = new
    {
        output.VersionId,
        EntityCount = output.Entities.Count,
        CategoryCount = output.Metadata.TotalCategoryCount,
        output.Metadata.AvailableMetrics,
        output.Aggregates.TotalCategoryCount
    };

    Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Processing complete. Artifacts written to processed/{output.VersionId}/");
}

static async Task HandlePublish(ProcessingPipeline pipeline, string[] args)
{
    var versionId = GetArgValue(args, "--version-id");
    if (string.IsNullOrEmpty(versionId))
    {
        await Console.Error.WriteLineAsync("ERROR: --version-id is required for publish");
        return;
    }

    await pipeline.PublishAsync(versionId);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        VersionId = versionId,
        Status = "Published",
        Message = $"Version '{versionId}' has been published and is ready for activation"
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task HandleList(IVersionRegistry registry)
{
    var versions = await registry.ListVersionsAsync();
    foreach (var v in versions)
    {
        var marker = v.Status == VersionStatus.Active ? " [ACTIVE]" :
                     v.Status == VersionStatus.Published ? " [PUBLISHED]" :
                     v.Status == VersionStatus.Processed ? " [PROCESSED]" :
                     v.Status == VersionStatus.Processing ? " [PROCESSING]" : "";
        var artifact = v.ProcessedArtifactPath is not null ? $" -> {v.ProcessedArtifactPath}" : "";
        Console.WriteLine($"{v.VersionId,-18} {v.Status,-12} {v.CreatedAt:yyyy-MM-dd HH:mm:ss}{marker}{artifact}");
    }
}

static async Task HandleShow(IVersionRegistry registry, string[] args)
{
    var versionId = GetArgValue(args, "--version-id");
    if (string.IsNullOrEmpty(versionId))
    {
        await Console.Error.WriteLineAsync("ERROR: --version-id is required for show");
        return;
    }

    var record = await registry.GetVersionAsync(versionId);
    if (record is null)
    {
        await Console.Error.WriteLineAsync($"Version '{versionId}' not found");
        return;
    }

    var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
    Console.WriteLine(json);
}

static async Task HandleInitDb(IVersionRegistry registry)
{
    await registry.InitializeSchemaAsync();
    Console.WriteLine("Database schema initialized successfully");
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage: ProcessingService <command> [options]

        Commands:
          process   --version-id <id>  Process a raw dataset version (Raw → Processing → Processed)
          publish   --version-id <id>  Publish a processed version (Processed → Published)
          list                          List all versions with processing status
          show      --version-id <id>  Show version details
          init-db                      Initialize the version registry schema

        Processing Pipeline:
          Normalize → Enrich → Aggregate → Validate → Publish

        Environment:
          STORAGE_POSTGRES_CONNECTION_STRING   PostgreSQL connection string
          ARTIFACT_ROOT_PATH                    Root path for artifact storage (default: temp dir)
        """);
}

static string? GetArgValue(string[] args, string flag)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == flag)
            return args[i + 1];
    }
    return null;
}

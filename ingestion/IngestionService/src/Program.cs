using System.Text.Json;
using IngestionService.Services;
using ServiceContract.VersionRegistry;

try
{
    var connectionString = Environment.GetEnvironmentVariable("STORAGE_POSTGRES_CONNECTION_STRING")
        ?? "Host=localhost;Database=corpus;Username=postgres;Password=postgres";
    var artifactRoot = Environment.GetEnvironmentVariable("ARTIFACT_ROOT_PATH")
        ?? Path.Combine(Path.GetTempPath(), "ingestion-artifacts");

    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    // When invoked via "dotnet run -- <command> <args>", args may include
    // a leading runtime argument prefix. Find the actual command.
    var cmdIndex = Array.FindIndex(args, a => a is "ingest" or "rollback" or "list" or "show" or "init-db");

    if (cmdIndex < 0)
    {
        Console.Error.WriteLine($"Unknown command. Use 'IngestionService' or 'dotnet run' with a command.");
        PrintUsage();
        return 1;
    }

    var command = args[cmdIndex];
    var cmdArgs = args.Skip(cmdIndex + 1).ToArray();

    await using var registry = new PostgresVersionRegistry(connectionString);
    var artifactStore = new LocalArtifactStore(artifactRoot);
    var service = new DatasetIngestionService(registry, artifactStore);

    switch (command)
    {
        case "ingest":
            await HandleIngest(service, cmdArgs);
            break;
        case "rollback":
            await HandleRollback(service, cmdArgs);
            break;
        case "list":
            await HandleList(service);
            break;
        case "show":
            await HandleShow(service, cmdArgs);
            break;
        case "init-db":
            await HandleInitDb(service);
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

static async Task HandleIngest(IDatasetIngestionService service, string[] args)
{
    var sourcePath = GetArgValue(args, "--source-path");
    if (string.IsNullOrEmpty(sourcePath))
    {
        await Console.Error.WriteLineAsync("ERROR: --source-path is required for ingest");
        return;
    }

    var metadata = GetArgValue(args, "--metadata");

    var response = await service.IngestAsync(new IngestRequest
    {
        SourcePath = sourcePath,
        Metadata = metadata
    });

    if (response.Status == VersionStatus.Failed)
    {
        await Console.Error.WriteLineAsync($"INGEST FAILED: {response.ErrorMessage}");
        return;
    }

    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
}

static async Task HandleRollback(IDatasetIngestionService service, string[] args)
{
    var versionId = GetArgValue(args, "--version-id");
    if (string.IsNullOrEmpty(versionId))
    {
        await Console.Error.WriteLineAsync("ERROR: --version-id is required for rollback");
        return;
    }

    var response = await service.RollbackToVersionAsync(versionId);
    Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task HandleList(IDatasetIngestionService service)
{
    var response = await service.ListVersionsAsync();
    foreach (var v in response.Versions)
    {
        var marker = v.Status == VersionStatus.Active ? " [ACTIVE]" : "";
        Console.WriteLine($"{v.VersionId,-18} {v.Status,-12} {v.CreatedAt:yyyy-MM-dd HH:mm:ss}{marker}");
    }
}

static async Task HandleShow(IDatasetIngestionService service, string[] args)
{
    var versionId = GetArgValue(args, "--version-id");
    if (string.IsNullOrEmpty(versionId))
    {
        await Console.Error.WriteLineAsync("ERROR: --version-id is required for show");
        return;
    }

    var record = await service.GetVersionAsync(versionId);
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

static async Task HandleInitDb(IDatasetIngestionService service)
{
    await service.InitializeAsync();
    Console.WriteLine("Database schema initialized successfully");
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage: IngestionService <command> [options]

        Commands:
          ingest    --source-path <path> [--metadata <json>]  Accept a new dataset drop
          rollback  --version-id <id>                          Rollback to a previous version
          list                                                  List all versions
          show      --version-id <id>                          Show version details
          init-db                                               Initialize the version registry schema

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

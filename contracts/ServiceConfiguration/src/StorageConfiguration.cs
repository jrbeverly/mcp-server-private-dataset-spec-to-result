namespace ServiceConfiguration;

public sealed record StorageConfiguration
{
    public required PostgresConfig ServingStore { get; init; }
    public required S3Config ArtifactStore { get; init; }

    public const string SectionName = "Storage";

    public sealed record PostgresConfig
    {
        public required string ConnectionString { get; init; }
        public string? Schema { get; init; }
    }

    public sealed record S3Config
    {
        public required string BucketName { get; init; }
        public string? Region { get; init; }
        public string? RawPrefix { get; init; }
        public string? ProcessedPrefix { get; init; }
    }

    public static StorageConfiguration FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("STORAGE_POSTGRES_CONNECTION_STRING")
            ?? throw new InvalidOperationException("STORAGE_POSTGRES_CONNECTION_STRING is required");
        var bucketName = Environment.GetEnvironmentVariable("STORAGE_S3_BUCKET_NAME")
            ?? throw new InvalidOperationException("STORAGE_S3_BUCKET_NAME is required");

        return new StorageConfiguration
        {
            ServingStore = new PostgresConfig
            {
                ConnectionString = connectionString,
                Schema = Environment.GetEnvironmentVariable("STORAGE_POSTGRES_SCHEMA") ?? "public"
            },
            ArtifactStore = new S3Config
            {
                BucketName = bucketName,
                Region = Environment.GetEnvironmentVariable("STORAGE_S3_REGION"),
                RawPrefix = Environment.GetEnvironmentVariable("STORAGE_S3_RAW_PREFIX") ?? "raw/",
                ProcessedPrefix = Environment.GetEnvironmentVariable("STORAGE_S3_PROCESSED_PREFIX") ?? "processed/"
            }
        };
    }
}

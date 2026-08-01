namespace ServiceContract.VersionRegistry;

public enum VersionStatus
{
    Raw,
    Processing,
    Processed,
    Published,
    Active,
    Failed,
    Superseded
}

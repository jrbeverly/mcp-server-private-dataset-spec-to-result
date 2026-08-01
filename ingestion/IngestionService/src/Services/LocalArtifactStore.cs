namespace IngestionService.Services;

public sealed class LocalArtifactStore : IArtifactStore
{
    private readonly string _rootPath;

    public LocalArtifactStore(string rootPath)
    {
        _rootPath = rootPath;
    }

    public async Task<string> StoreRawArtifactAsync(
        string versionId, string sourcePath,
        CancellationToken cancellationToken = default)
    {
        var versionDir = Path.Combine(_rootPath, "raw", versionId);

        if (!Directory.Exists(versionDir))
            Directory.CreateDirectory(versionDir);

        if (Directory.Exists(sourcePath))
        {
            await CopyDirectoryAsync(sourcePath, versionDir, cancellationToken);
            return versionDir;
        }

        if (File.Exists(sourcePath))
        {
            var destFile = Path.Combine(versionDir, Path.GetFileName(sourcePath));
            EnsureDirectoryExists(versionDir);
            File.Copy(sourcePath, destFile, overwrite: true);
            await Task.CompletedTask;
            return destFile;
        }

        throw new InvalidOperationException(
            $"Source path '{sourcePath}' does not exist as a file or directory");
    }

    private static async Task CopyDirectoryAsync(
        string sourceDir, string destDir,
        CancellationToken cancellationToken)
    {
        EnsureDirectoryExists(destDir);

        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));

        foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var dest = filePath.Replace(sourceDir, destDir);
            File.Copy(filePath, dest, overwrite: true);
        }

        await Task.CompletedTask;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}

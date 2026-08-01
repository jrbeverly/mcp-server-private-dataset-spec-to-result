namespace IngestionService.Validation;

public static class DatasetValidator
{
    public static ValidationResult Validate(string sourcePath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            errors.Add("Source path is required");
            return new ValidationResult(false, errors);
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            errors.Add($"Source path '{sourcePath}' does not exist");
            return new ValidationResult(false, errors);
        }

        if (Directory.Exists(sourcePath))
        {
            var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                errors.Add($"Source directory '{sourcePath}' is empty — no files to ingest");
        }
        else if (File.Exists(sourcePath))
        {
            var fileInfo = new FileInfo(sourcePath);
            if (fileInfo.Length == 0)
                errors.Add($"Source file '{sourcePath}' is empty");
        }

        return errors.Count == 0
            ? new ValidationResult(true, Array.Empty<string>())
            : new ValidationResult(false, errors);
    }
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

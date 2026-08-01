using IngestionService.Validation;
using Xunit;

namespace IngestionService.Tests.Validation;

public class DatasetValidatorTests
{
    [Fact]
    public void Validate_EmptyPath_ReturnsInvalid()
    {
        var result = DatasetValidator.Validate("");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required"));
    }

    [Fact]
    public void Validate_WhitespacePath_ReturnsInvalid()
    {
        var result = DatasetValidator.Validate("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NonExistentPath_ReturnsInvalid()
    {
        var result = DatasetValidator.Validate("/nonexistent/path/dataset");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_ValidFile_ReturnsValid()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content");

            var result = DatasetValidator.Validate(tempFile);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsInvalid()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // temp file is empty
            var result = DatasetValidator.Validate(tempFile);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("empty"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_ValidDirectory_ReturnsValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "data.csv"), "col1,col2\nv1,v2");

            var result = DatasetValidator.Validate(tempDir);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_EmptyDirectory_ReturnsInvalid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);

            var result = DatasetValidator.Validate(tempDir);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("empty"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

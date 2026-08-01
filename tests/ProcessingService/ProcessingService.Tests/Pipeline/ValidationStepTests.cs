using ProcessingService.Models;
using ProcessingService.Pipeline;
using Xunit;

namespace ProcessingService.Tests.Pipeline;

public class ValidationStepTests
{
    [Fact]
    public void Validate_ValidEntities_Passes()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "Entity 1", Category = "Tech", Properties = new Dictionary<string, string> { ["key"] = "value" } }
        };

        var result = ValidationStep.Validate(entities);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyList_Fails()
    {
        var result = ValidationStep.Validate(new List<CanonicalEntity>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("zero"));
    }

    [Fact]
    public void Validate_DuplicateIds_Fails()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "A" },
            new() { EntityId = "e1", Name = "B" }
        };

        var result = ValidationStep.Validate(entities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_AllUnnamed_Fails()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "" },
            new() { EntityId = "e2", Name = "" },
            new() { EntityId = "e3", Name = "" }
        };

        var result = ValidationStep.Validate(entities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validate_NoData_Fails()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "Test" }
        };

        var result = ValidationStep.Validate(entities);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("zero properties"));
    }

    [Fact]
    public void Validate_MostlyNamed_Passes()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "Named", Properties = new Dictionary<string, string> { ["k"] = "v" } },
            new() { EntityId = "e2", Name = "", Properties = new Dictionary<string, string> { ["k"] = "v" } }
        };

        var result = ValidationStep.Validate(entities);

        Assert.True(result.IsValid);
    }
}

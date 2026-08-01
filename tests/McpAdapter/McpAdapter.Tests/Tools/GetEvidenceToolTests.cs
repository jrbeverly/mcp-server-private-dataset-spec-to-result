using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class GetEvidenceToolTests
{
    private static IMcpTool CreateTool()
        => new GetEvidenceTool(new QueryServiceClient(new HttpClient { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public void Tool_Name_Is_GetEvidence()
    {
        Assert.Equal("get_evidence", CreateTool().Name);
    }

    [Fact]
    public void Tool_Has_Description()
    {
        Assert.NotEmpty(CreateTool().Description);
    }

    [Fact]
    public void InputSchema_Requires_EntityId()
    {
        var required = CreateTool().InputSchema["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains("entityId", required!.Select(n => (string?)n));
    }

    [Fact]
    public void InputSchema_Defines_EvidenceType_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["evidenceType"]);
    }

    [Fact]
    public void InputSchema_Defines_Limit_Property_With_Range()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        var limit = props!["limit"]?.AsObject();
        Assert.NotNull(limit);
        Assert.Equal("integer", (string?)limit!["type"]);
        Assert.Equal(1, (int?)limit!["minimum"]);
        Assert.Equal(100, (int?)limit!["maximum"]);
    }
}

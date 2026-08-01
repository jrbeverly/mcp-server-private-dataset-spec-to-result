using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class DrillDownToolTests
{
    private static IMcpTool CreateTool()
        => new DrillDownTool(new QueryServiceClient(new HttpClient { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public void Tool_Name_Is_DrillDown()
    {
        Assert.Equal("drill_down", CreateTool().Name);
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
    public void InputSchema_Defines_EntityId_As_String()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.Equal("string", (string?)props!["entityId"]?["type"]);
    }
}

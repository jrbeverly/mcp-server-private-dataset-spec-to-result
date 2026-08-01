using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class AggregateToolTests
{
    private static IMcpTool CreateTool()
        => new AggregateTool(new QueryServiceClient(new HttpClient { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public void Tool_Name_Is_Aggregate()
    {
        Assert.Equal("aggregate", CreateTool().Name);
    }

    [Fact]
    public void Tool_Has_Description()
    {
        Assert.NotEmpty(CreateTool().Description);
    }

    [Fact]
    public void InputSchema_Requires_Metric()
    {
        var required = CreateTool().InputSchema["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains("metric", required!.Select(n => (string?)n));
    }

    [Fact]
    public void InputSchema_Defines_GroupBy_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["groupBy"]);
    }

    [Fact]
    public void InputSchema_Defines_Sort_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["sort"]);
    }
}

using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class DiscoverToolTests
{
    private static IMcpTool CreateTool()
        => new DiscoverTool(new QueryServiceClient(new HttpClient { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public void Tool_Name_Is_Discover()
    {
        Assert.Equal("discover", CreateTool().Name);
    }

    [Fact]
    public void Tool_Has_Description()
    {
        Assert.NotEmpty(CreateTool().Description);
    }

    [Fact]
    public void InputSchema_Is_Object_Type()
    {
        Assert.Equal("object", (string?)CreateTool().InputSchema["type"]);
    }

    [Fact]
    public void InputSchema_Defines_Query_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["query"]);
        Assert.Equal("string", (string?)props["query"]!["type"]);
    }

    [Fact]
    public void InputSchema_Defines_Category_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["category"]);
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

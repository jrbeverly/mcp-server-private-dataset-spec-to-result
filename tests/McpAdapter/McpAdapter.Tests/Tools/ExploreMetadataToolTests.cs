using McpAdapter.Infrastructure;
using McpAdapter.Tools;
using Xunit;

namespace McpAdapter.Tests.Tools;

public class ExploreMetadataToolTests
{
    private static IMcpTool CreateTool()
        => new ExploreMetadataTool(new QueryServiceClient(new HttpClient { BaseAddress = new Uri("http://localhost") }));

    [Fact]
    public void Tool_Name_Is_ExploreMetadata()
    {
        Assert.Equal("explore_metadata", CreateTool().Name);
    }

    [Fact]
    public void Tool_Has_Description()
    {
        Assert.NotEmpty(CreateTool().Description);
    }

    [Fact]
    public void InputSchema_Has_No_Required_Fields()
    {
        Assert.Null(CreateTool().InputSchema["required"]);
    }

    [Fact]
    public void InputSchema_Defines_Category_Property()
    {
        var props = CreateTool().InputSchema["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props!["category"]);
    }
}

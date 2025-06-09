using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Xunit;
using Moq;
using Dynamics.Mcp;

namespace Dynamics.Mcp.Tests;

/// <summary>
/// Unit tests for the DynamicToolRegistry class.
/// Verifies tool registration, execution, and refresh functionality.
/// </summary>
public class DynamicToolRegistryTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<DynamicToolRegistry>> _mockLogger;
    private readonly DynamicToolRegistry _registry;

    public DynamicToolRegistryTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<DynamicToolRegistry>>();
        _registry = new DynamicToolRegistry(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterDynamicsEndpoint_WithInvalidParameters_HandlesGracefully()
    {
        // Arrange
        var validBaseUrl = "https://contoso.api.crm.dynamics.com";
        var validBearerToken = "test-token";
        
        // Create a real HttpClient for this test
        var httpClient = new HttpClient();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Test with invalid URL - should return error result rather than throw
        var result1 = await _registry.RegisterDynamicsEndpoint("", validBearerToken);
        Assert.False(result1.Success);
        Assert.Contains("Base URL", result1.Message);
        
        var result2 = await _registry.RegisterDynamicsEndpoint(validBaseUrl, "");
        Assert.False(result2.Success);
        Assert.Contains("Bearer token", result2.Message);
        
        // Clean up
        httpClient.Dispose();
    }

    [Fact]
    public async Task ListDynamicTools_WithEmptyRegistry_ReturnsEmptyResult()
    {
        // Act
        var result = await _registry.ListDynamicTools();

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Endpoints);
        Assert.Equal(0, result.TotalToolCount);
    }

    [Fact]
    public async Task ExecuteDynamicTool_WithInvalidToolName_ReturnsError()
    {
        // Act
        var result = await _registry.ExecuteDynamicTool("nonexistent_tool", "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task UnregisterEndpoint_WithNonexistentEndpoint_ReturnsSuccess()
    {
        // Act
        var result = await _registry.UnregisterEndpoint("nonexistent");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.RemovedToolCount);
    }

    [Fact]
    public async Task RefreshEndpointTools_WithNonexistentEndpoint_ReturnsError()
    {
        // Act
        var result = await _registry.RefreshEndpointTools("nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }
}

/// <summary>
/// Integration tests for the complete MCP server setup.
/// </summary>
public class McpServerIntegrationTests
{
    [Fact]
    public void HostBuilder_WithDynamicToolRegistry_ConfiguresCorrectly()
    {
        // Arrange & Act
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddHttpClient()
            .AddSingleton<DynamicToolRegistry>()
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        
        // Assert
        var registry = host.Services.GetService<DynamicToolRegistry>();
        Assert.NotNull(registry);
    }
}

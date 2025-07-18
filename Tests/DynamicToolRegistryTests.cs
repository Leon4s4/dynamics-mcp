using Microsoft.Extensions.Configuration;
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
/// Verifies initialization, tool listing, and execution functionality.
/// </summary>
public class DynamicToolRegistryTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public DynamicToolRegistryTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Initialize the static registry with mocked dependencies
        DynamicToolRegistry.Initialize(_mockHttpClientFactory.Object, _mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task InitializeAsync_WithoutConnectionString_HandlesGracefully()
    {
        // Arrange - No connection string setup, should return null
        
        // Act & Assert - Should not throw, just handle gracefully
        var exception = await Record.ExceptionAsync(async () => await DynamicToolRegistry.InitializeAsync());
        
        // Should not throw an exception
        Assert.Null(exception);
    }

    [Fact]
    public async Task GetEndpointStatus_WithoutInitialization_ReturnsNotInitialized()
    {
        // Act
        var result = await DynamicToolRegistry.GetEndpointStatus();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not initialized", result.Message);
    }

    [Fact]
    public async Task ListDynamicTools_WithoutInitialization_ReturnsNotInitialized()
    {
        // Act
        var result = await DynamicToolRegistry.ListDynamicTools();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not initialized", result.Message);
    }

    [Fact]
    public async Task ExecuteDynamicTool_WithoutInitialization_ReturnsNotInitialized()
    {
        // Act
        var result = await DynamicToolRegistry.ExecuteDynamicTool("test_tool", "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not initialized", result.Message);
    }

    [Fact]
    public async Task RefreshTools_WithoutInitialization_ReturnsNotInitialized()
    {
        // Act
        var result = await DynamicToolRegistry.RefreshTools();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not initialized", result.Message);
    }

    [Fact]
    public void DynamicsConnectionString_Parse_HandlesClientCredentialsConnectionString()
    {
        // Arrange
        var connectionString = "AuthType=OAuth;Url=https://contoso.crm.dynamics.com;ClientId=test-client;ClientSecret=test-secret;LoginPrompt=Never";

        // Act
        var result = DynamicsConnectionString.Parse(connectionString);

        // Assert
        Assert.Equal("OAuth", result.AuthType);
        Assert.Equal("https://contoso.crm.dynamics.com", result.Url);
        Assert.Equal("test-client", result.ClientId);
        Assert.Equal("test-secret", result.ClientSecret);
        Assert.Equal("Never", result.LoginPrompt);
        Assert.True(result.IsClientCredentials);
        Assert.False(result.IsUsernamePassword);
    }

    [Fact]
    public void DynamicsConnectionString_Parse_HandlesUsernamePasswordConnectionString()
    {
        // Arrange
        var connectionString = "AuthType=OAuth;Url=https://contoso.crm.dynamics.com;Username=user@contoso.com;Password=mypassword;LoginPrompt=Never";

        // Act
        var result = DynamicsConnectionString.Parse(connectionString);

        // Assert
        Assert.Equal("OAuth", result.AuthType);
        Assert.Equal("https://contoso.crm.dynamics.com", result.Url);
        Assert.Equal("user@contoso.com", result.Username);
        Assert.Equal("mypassword", result.Password);
        Assert.Equal("Never", result.LoginPrompt);
        Assert.False(result.IsClientCredentials);
        Assert.True(result.IsUsernamePassword);
    }

    [Fact]
    public void DynamicsConnectionString_Parse_HandlesEmptyConnectionString()
    {
        // Arrange
        var connectionString = "";

        // Act
        var result = DynamicsConnectionString.Parse(connectionString);

        // Assert
        Assert.Equal("OAuth", result.AuthType);
        Assert.Equal("Never", result.LoginPrompt);
        Assert.Empty(result.Url);
        Assert.Empty(result.ClientId);
        Assert.Empty(result.ClientSecret);
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
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        
        // Assert
        var httpClientFactory = host.Services.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);
    }
}

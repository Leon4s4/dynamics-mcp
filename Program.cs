using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Dynamics.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configure services
builder.Services
    .AddHttpClient() // Add HttpClient factory for Dynamics API calls
    .AddSingleton<DynamicToolRegistry>() // Register the dynamic tool registry
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

Console.WriteLine("Starting MCP server for Microsoft Dynamics 365");
Console.WriteLine("Available MCP tools:");
Console.WriteLine("- RegisterDynamicsEndpoint: Register a Dynamics 365 instance");
Console.WriteLine("- ListDynamicTools: List all generated tools");
Console.WriteLine("- ExecuteDynamicTool: Execute a generated tool");
Console.WriteLine("- RefreshEndpointTools: Refresh tools for an endpoint");
Console.WriteLine("- UnregisterEndpoint: Remove an endpoint and its tools");

await builder.Build().RunAsync();
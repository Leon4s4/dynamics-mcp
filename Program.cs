using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? "http://localhost:4000/dynamicsql";
var headersJson = Environment.GetEnvironmentVariable("HEADERS") ?? "{}";
Dictionary<string, string> headers;
try
{
    headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? new();
}
catch
{
    headers = new();
}

var name = Environment.GetEnvironmentVariable("NAME") ?? "mcp-dynamicsql";
var schemaPath = Environment.GetEnvironmentVariable("SCHEMA");
if (!string.IsNullOrWhiteSpace(schemaPath) && File.Exists(schemaPath))
{
    var schemaContent = File.ReadAllText(schemaPath);
    builder.Services.AddSingleton(schemaContent);
}

Console.WriteLine($"Starting MCP server: {name}");

await builder.Build().RunAsync();
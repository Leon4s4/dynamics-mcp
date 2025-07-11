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

Console.WriteLine("=================================================================");
Console.WriteLine("🚀 Microsoft Dynamics 365 MCP Server");
Console.WriteLine("    Dynamic schema introspection with comprehensive tooling");
Console.WriteLine("=================================================================");
Console.WriteLine();
Console.WriteLine("📋 MCP TOOLS (9 available):");
Console.WriteLine("   • GetDynamicsStatus      - Comprehensive connection diagnostics");
Console.WriteLine("   • ListDynamicsEntities   - Complete entity discovery with capabilities");
Console.WriteLine("   • CreateDynamicsRecord   - Advanced record creation with validation");
Console.WriteLine("   • ReadDynamicsRecord     - Complete record retrieval with formatting");
Console.WriteLine("   • UpdateDynamicsRecord   - Selective field updates with validation");
Console.WriteLine("   • DeleteDynamicsRecord   - Safe deletion with dependency checking");
Console.WriteLine("   • ListDynamicsRecords    - Advanced querying with OData filtering");
Console.WriteLine("   • SearchDynamicsRecords  - Field-specific search with fuzzy matching");
Console.WriteLine("   • RefreshDynamicsSchema  - Complete schema refresh and tool regeneration");
Console.WriteLine();
Console.WriteLine("💡 MCP PROMPTS (6 available):");
Console.WriteLine("   • CreateRecordPrompt     - Intelligent record creation guidance");
Console.WriteLine("   • BuildFilterPrompt      - Expert OData filter expression building");
Console.WriteLine("   • TransformDataPrompt    - Advanced data transformation assistance");
Console.WriteLine("   • TroubleshootPrompt     - Comprehensive error analysis and resolution");
Console.WriteLine("   • OptimizeQueryPrompt    - Performance optimization recommendations");
Console.WriteLine("   • BulkOperationPrompt    - Enterprise-grade bulk operation planning");
Console.WriteLine();
Console.WriteLine("📚 MCP RESOURCES (4 available):");
Console.WriteLine("   • GetEntitySchema        - Complete entity schema with relationships");
Console.WriteLine("   • GetEntitiesSummary     - Full environment entity catalog");
Console.WriteLine("   • GetApiDocumentation    - Comprehensive API reference");
Console.WriteLine("   • GetOperationExamples   - Real-world operation examples");
Console.WriteLine();
Console.WriteLine("⚡ Server initializing with connection string from environment...");

var app = builder.Build();

// Initialize the Dynamics endpoint at startup
var registry = app.Services.GetRequiredService<DynamicToolRegistry>();
await registry.InitializeAsync();

await app.RunAsync();
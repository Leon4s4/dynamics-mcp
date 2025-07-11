# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build and Run
- `dotnet build` - Build the project
- `dotnet run` - Start the MCP server in development mode
- `dotnet run --configuration Release` - Run in production mode
- `./launch.sh` - Start server using launch script (for MCP client integration)

### Testing
- `dotnet test` - Run all unit tests
- `dotnet test --verbosity normal` - Run tests with verbose output
- `./test-server.sh` - Test the MCP server functionality

### Docker
- `docker-compose up` - Start with Docker Compose
- `docker-compose up --build` - Build and start with Docker

### Publishing
- `dotnet publish -c Release` - Build for production deployment

## Architecture

This is a **Dynamic Model Context Protocol (MCP) Server** for Microsoft Dynamics 365 that automatically generates tools by introspecting the Dataverse schema at runtime using a connection string configuration.

### Core Components

1. **DynamicToolRegistry** (`DynamicToolRegistry.cs`): The main service that handles:
   - Single Dynamics 365 endpoint initialization from connection string
   - Real-time schema introspection via Dataverse Web API
   - Dynamic tool generation for all discovered entities
   - Tool execution with proper HTTP client management

2. **DynamicsConnectionString**: Connection string parser that handles:
   - Standard CRM connection string format parsing
   - OAuth client credentials extraction
   - Environment variable configuration support

3. **DynamicsMcpTools** (`DynamicsMcpTools.cs`): MCP tools following C# SDK patterns:
   - High-level operations for creating, reading, updating, deleting records
   - Entity listing and schema introspection
   - Proper error handling and structured responses

4. **DynamicsMcpPrompts** (`DynamicsMcpPrompts.cs`): MCP prompts for common scenarios:
   - Record creation guidance
   - OData filter building
   - Data transformation assistance
   - Troubleshooting help
   - Query optimization
   - Bulk operation planning

5. **DynamicsMcpResources** (`DynamicsMcpResources.cs`): MCP resources for metadata:
   - Entity schemas and field definitions
   - API documentation
   - Operation examples
   - Complete entity summaries

6. **Program.cs**: MCP server entry point with:
   - Microsoft.Extensions.Hosting application builder
   - HttpClient factory registration for API calls
   - MCP server configuration with stdio transport
   - Automatic endpoint initialization at startup

### Configuration

The server uses a connection string from environment variables:
- `DYNAMICS_CONNECTION_STRING` environment variable, or
- `ConnectionStrings:Dynamics` in configuration

**Two authentication methods supported:**

#### Method 1: Username/Password (Simpler)
```bash
export DYNAMICS_CONNECTION_STRING="AuthType=OAuth;Url=https://yourorg.crm.dynamics.com;Username=your-username@yourorg.com;Password=your-password;LoginPrompt=Never"
```

#### Method 2: Client Credentials (App Registration)
```bash
export DYNAMICS_CONNECTION_STRING="AuthType=OAuth;Url=https://yourorg.crm.dynamics.com;ClientId=your-client-id;ClientSecret=your-client-secret;LoginPrompt=Never"
```

**Which method to use?**
- **Username/Password**: Easier setup, uses your regular CRM login credentials
- **Client Credentials**: More secure for production, requires Azure AD app registration

### Tool Generation Pattern

The server automatically generates tools following this naming convention:
- `dynamics_create_{entity}` - Create new records
- `dynamics_read_{entity}` - Read records by ID
- `dynamics_update_{entity}` - Update existing records
- `dynamics_delete_{entity}` - Delete records
- `dynamics_list_{entity}` - List records with filtering
- `dynamics_search_{entity}_by_{field}` - Search by specific fields

### Key API Endpoints Used

The server introspects these Dataverse endpoints:
- `/api/data/v9.2/EntityDefinitions` - Discover all entities
- `/api/data/v9.2/EntityDefinitions(LogicalName='{entity}')/Attributes` - Get entity schema
- Standard CRUD operations on `/api/data/v9.2/{EntitySetName}`

### MCP Tools Available

1. **GetDynamicsStatus** - Check connection status and health
2. **ListDynamicsEntities** - List all entities and their operations
3. **CreateDynamicsRecord** - Create new records in any entity
4. **ReadDynamicsRecord** - Read records by ID from any entity
5. **UpdateDynamicsRecord** - Update existing records in any entity
6. **DeleteDynamicsRecord** - Delete records from any entity
7. **ListDynamicsRecords** - List records with filtering and pagination
8. **SearchDynamicsRecords** - Search records by field values
9. **RefreshDynamicsSchema** - Refresh schema and regenerate tools

### MCP Prompts Available

1. **CreateRecordPrompt** - Generate prompts for creating records
2. **BuildFilterPrompt** - Generate OData filter expressions
3. **TransformDataPrompt** - Transform data for Dynamics operations
4. **TroubleshootPrompt** - Help troubleshoot operation failures
5. **OptimizeQueryPrompt** - Optimize query performance
6. **BulkOperationPrompt** - Plan bulk operations

### MCP Resources Available

1. **GetEntitySchema** - Entity schema and metadata
2. **GetEntitiesSummary** - Summary of all entities
3. **GetApiDocumentation** - Complete API documentation
4. **GetOperationExamples** - Common operation examples

### Data Flow

1. Server starts and reads connection string from environment variables
2. Server parses connection string and obtains OAuth access token
3. Server introspects Dataverse schema via API calls
4. Tools are dynamically generated and cached in memory
5. Client can list, execute, or refresh tools as needed
6. All tool executions are proxied through the server to Dataverse API

### Authentication

Supports two OAuth 2.0 flows automatically determined by the connection string:

1. **Resource Owner Password Credentials Flow** (Username/Password)
   - Uses your regular Dynamics 365 login credentials
   - Automatically uses PowerApps Client ID for authentication
   - Perfect for development and testing scenarios

2. **Client Credentials Flow** (ClientId/ClientSecret)
   - Uses Azure AD app registration credentials
   - Recommended for production environments
   - Provides better security and auditing capabilities

The access token is obtained automatically at startup and used for all subsequent Dataverse API calls.

## Technology Stack

- .NET 9.0 with C# nullable reference types enabled
- ModelContextProtocol package for MCP server implementation
- Microsoft.Extensions.Hosting for application hosting
- HttpClient with factory pattern for API calls
- System.Text.Json for JSON serialization
- xUnit + Moq for unit testing
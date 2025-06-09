# Dynamics 365 MCP Server

A dynamic Model Context Protocol (MCP) server for Microsoft Dynamics 365 that automatically generates tools by introspecting the Dataverse schema.

## Table of Contents

- [Quick Start](#quick-start)
- [Features](#features)
- [Architecture](#architecture)
- [Technology Stack](#technology-stack)
- [Installation](#installation)
- [Usage](#usage)
- [Available MCP Tools](#available-mcp-tools)
- [Generated Tool Operations](#generated-tool-operations)
- [Authentication](#authentication)
- [API Endpoints Used](#api-endpoints-used)
- [Example Workflow](#example-workflow)
- [Error Handling](#error-handling)
- [Logging](#logging)
- [Security Considerations](#security-considerations)
- [Development](#development)
- [Troubleshooting](#troubleshooting)
- [License](#license)
- [Support](#support)

## Quick Start

1. **Clone and build:**
   ```bash
   git clone <repository-url>
   cd dynamics-mcp
   dotnet build
   ```

2. **Start the server:**
   ```bash
   dotnet run
   ```

3. **Register your Dynamics 365 instance:**
   ```json
   {
     "tool": "RegisterDynamicsEndpoint",
     "arguments": {
       "baseUrl": "https://your-org.api.crm.dynamics.com",
       "bearerToken": "your-oauth-token"
     }
   }
   ```

4. **List generated tools:**
   ```json
   {
     "tool": "ListDynamicTools",
     "arguments": {}
   }
   ```

5. **Use the tools to interact with your Dynamics 365 data!**

## Features

- **Dynamic Tool Generation**: Automatically creates MCP tools for all Dynamics 365 entities
- **Real-time Schema Introspection**: Connects to Dataverse Web API to discover entities and fields
- **Standard CRUD Operations**: Generates Create, Read, Update, Delete, and List tools for each entity
- **Smart Search Tools**: Creates search tools for key fields in each entity
- **Multi-Instance Support**: Manage multiple Dynamics 365 environments simultaneously
- **OAuth 2.0 Authentication**: Secure authentication using bearer tokens

## Architecture

The server generates tools following this naming convention:
```
dynamics_{operation}_{entity}[_by_{field}]
```

Examples:
- `dynamics_create_account` - Create a new account record
- `dynamics_read_contact` - Read a contact by ID
- `dynamics_list_opportunity` - List opportunities with filtering
- `dynamics_search_account_by_name` - Search accounts by name

## Technology Stack

- **.NET 9.0**: Modern cross-platform framework
- **System.Text.Json**: High-performance JSON serialization
- **HttpClient**: HTTP communications with Dataverse API
- **Microsoft.Extensions.Hosting**: Application hosting and dependency injection
- **Microsoft.Extensions.Logging**: Structured logging
- **ModelContextProtocol**: MCP server implementation
- **xUnit & Moq**: Unit testing framework

## Installation

### Prerequisites

- .NET 9.0 SDK or later
- Access to a Microsoft Dynamics 365 instance
- OAuth 2.0 bearer token for authentication

### Setup

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd dynamics-mcp
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run tests (optional):
   ```bash
   dotnet test
   ```

## Usage

### Starting the MCP Server

1. **Development mode:**
   ```bash
   dotnet run
   ```

2. **Using the launch script:**
   ```bash
   ./launch.sh
   ```

3. **Production mode:**
   ```bash
   dotnet run --configuration Release
   ```

4. **Docker:**
   ```bash
   docker-compose up
   ```

The server will start and listen for MCP requests on standard input/output.

### Connecting from MCP Clients

Add the following configuration to your MCP client configuration file:

```json
{
  "mcpServers": {
    "dynamics365": {
      "command": "/path/to/dynamics-mcp/launch.sh",
      "args": [],
      "env": {},
      "description": "Microsoft Dynamics 365 MCP Server - Dynamic tool registry for Dataverse API integration"
    }
  }
}
```

A sample configuration file (`mcp-client-config.json`) is included in the repository.

### Available MCP Tools

#### 1. RegisterDynamicsEndpoint
Registers a Dynamics 365 instance and generates tools for all entities.

**Parameters:**
- `baseUrl` (string): Base URL of the Dynamics 365 instance
- `bearerToken` (string): OAuth 2.0 bearer token
- `prefix` (string, optional): Prefix for tool names

**Example:**
```json
{
  "baseUrl": "https://contoso.api.crm.dynamics.com",
  "bearerToken": "eyJ0eXAiOiJKV1QiLCJhbGc...",
  "prefix": "contoso"
}
```

#### 2. ListDynamicTools
Lists all dynamically generated tools grouped by endpoint and entity.

**Returns:**
- List of endpoints with their generated tools
- Total tool count

#### 3. ExecuteDynamicTool
Executes a specific generated tool with the provided input.

**Parameters:**
- `toolName` (string): Name of the tool to execute
- `inputJson` (string): JSON input parameters

**Example:**
```json
{
  "toolName": "dynamics_create_account",
  "inputJson": "{\"name\": \"Contoso Corp\", \"websiteurl\": \"https://contoso.com\"}"
}
```

#### 4. RefreshEndpointTools
Re-introspects schema and regenerates tools for an endpoint.

**Parameters:**
- `endpointId` (string): ID of the endpoint to refresh

#### 5. UnregisterEndpoint
Removes an endpoint and all its associated tools.

**Parameters:**
- `endpointId` (string): ID of the endpoint to unregister

## Generated Tool Operations

For each entity, the following tools are automatically generated:

### CRUD Operations
- **Create**: `dynamics_create_{entity}` - Create new records
- **Read**: `dynamics_read_{entity}` - Read a record by ID
- **Update**: `dynamics_update_{entity}` - Update existing records
- **Delete**: `dynamics_delete_{entity}` - Delete records
- **List**: `dynamics_list_{entity}` - List records with filtering

### Search Operations
- **Search by Field**: `dynamics_search_{entity}_by_{field}` - Search records by specific fields

## Authentication

The server uses OAuth 2.0 bearer tokens to authenticate with the Dataverse Web API. To obtain a token:

1. Register an application in Azure Active Directory
2. Grant permissions to access Dynamics 365
3. Use the appropriate OAuth 2.0 flow to obtain a token
4. Pass the token when registering an endpoint

## API Endpoints Used

The server introspects the following Dataverse endpoints:

- `GET /api/data/v9.2/EntityDefinitions` - List all entities
- `GET /api/data/v9.2/EntityDefinitions(LogicalName='{entity}')/Attributes` - Get entity attributes
- `GET /api/data/v9.2/EntityDefinitions(LogicalName='{entity}')/BoundActions` - Get bound actions
- `GET /api/data/v9.2/EntityDefinitions(LogicalName='{entity}')/BoundFunctions` - Get bound functions

## Example Workflow

1. **Register a Dynamics 365 instance:**
   ```json
   {
     "tool": "RegisterDynamicsEndpoint",
     "arguments": {
       "baseUrl": "https://contoso.api.crm.dynamics.com",
       "bearerToken": "your-oauth-token"
     }
   }
   ```

2. **List available tools:**
   ```json
   {
     "tool": "ListDynamicTools",
     "arguments": {}
   }
   ```

3. **Create a new account:**
   ```json
   {
     "tool": "ExecuteDynamicTool",
     "arguments": {
       "toolName": "dynamics_create_account",
       "inputJson": "{\"name\": \"New Customer\", \"websiteurl\": \"https://example.com\"}"
     }
   }
   ```

4. **Search for contacts:**
   ```json
   {
     "tool": "ExecuteDynamicTool",
     "arguments": {
       "toolName": "dynamics_search_contact_by_lastname",
       "inputJson": "{\"lastname\": \"Smith\"}"
     }
   }
   ```

## Error Handling

The server implements comprehensive error handling:
- **Authentication errors**: Invalid or expired tokens
- **API errors**: Dataverse API failures
- **Validation errors**: Invalid input parameters
- **Network errors**: Connection timeouts and failures

All errors are returned with descriptive messages and appropriate error codes.

## Logging

The server provides detailed logging for:
- Endpoint registration and management
- Tool generation and execution
- API calls and responses
- Error conditions

## Security Considerations

- **No hardcoded credentials**: All authentication uses provided bearer tokens
- **Token validation**: Tokens are validated against the Dataverse API
- **Input sanitization**: All input parameters are validated and sanitized
- **HTTPS enforcement**: All API calls use HTTPS

## Development

### Project Structure

```
dynamics-mcp/
├── DynamicToolRegistry.cs      # Core tool registry implementation
├── Program.cs                  # MCP server entry point
├── dynamics-mcp.csproj         # Project file
├── README.md                   # This file
├── requirements.md             # Project requirements
├── Dockerfile                  # Docker container configuration
├── compose.yaml               # Docker Compose configuration
├── launch.sh                  # Launch script for MCP clients
├── test-server.sh             # Server testing script
├── mcp-client-config.json     # Sample MCP client configuration
├── examples.json              # Usage examples
└── Tests/
    └── DynamicToolRegistryTests.cs  # Unit tests
```

### Running Tests

```bash
dotnet test
```

For verbose test output:
```bash
dotnet test --verbosity normal
```

### Building for Production

```bash
dotnet publish -c Release
```

### Docker Deployment

Build and run with Docker:
```bash
docker-compose up --build
```

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

### Debugging

Enable detailed logging by setting the log level:
```bash
export DOTNET_ENVIRONMENT=Development
dotnet run
```

## Troubleshooting

### Common Issues

1. **"Bearer token cannot be null or empty"**
   - Ensure you have a valid OAuth 2.0 bearer token
   - Check that the token hasn't expired
   - Verify the token has proper Dynamics 365 permissions

2. **"Connection refused" or network errors**
   - Verify the Dynamics 365 base URL is correct
   - Ensure your network can reach the Dynamics 365 instance
   - Check if there are any firewall restrictions

3. **"Unauthorized" errors**
   - Verify the bearer token is valid and not expired
   - Check that the token has appropriate permissions for Dataverse API access
   - Ensure the application registration in Azure AD is configured correctly

4. **No tools generated after registration**
   - Check the server logs for any introspection errors
   - Verify the user has read permissions on entity metadata
   - Try using the `RefreshEndpointTools` function

### Getting OAuth 2.0 Tokens

To obtain a bearer token for Dynamics 365:

1. **Register an application in Azure Active Directory:**
   - Go to Azure Portal > Azure Active Directory > App registrations
   - Create a new registration
   - Note the Application (client) ID

2. **Configure API permissions:**
   - Add "Dynamics CRM" > "user_impersonation" permission
   - Grant admin consent

3. **Get a token using OAuth 2.0 flow:**
   ```bash
   # Example using device code flow
   curl -X POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/devicecode \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "client_id={client_id}&scope=https://org.api.crm.dynamics.com/.default"
   ```

For production scenarios, use the appropriate OAuth 2.0 flow for your application type.

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Support

For issues and questions:
- Create an issue in the GitHub repository
- Check the troubleshooting section above
- Review the server logs for detailed error information

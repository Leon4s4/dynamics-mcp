using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Configuration;

namespace Dynamics.Mcp;

/// <summary>
/// Connection string parser for Dynamics 365 CRM connection strings.
/// </summary>
public class DynamicsConnectionString
{
    public string Url { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthType { get; set; } = "OAuth";
    public string LoginPrompt { get; set; } = "Never";
    
    /// <summary>
    /// Determines if this is a Client Credentials flow (ClientId + ClientSecret)
    /// </summary>
    public bool IsClientCredentials => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
    
    /// <summary>
    /// Determines if this is a Resource Owner Password flow (Username + Password)
    /// </summary>
    public bool IsUsernamePassword => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public static DynamicsConnectionString Parse(string connectionString)
    {
        var result = new DynamicsConnectionString();
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();
                
                switch (key.ToLowerInvariant())
                {
                    case "url":
                        result.Url = value;
                        break;
                    case "clientid":
                        result.ClientId = value;
                        break;
                    case "clientsecret":
                        result.ClientSecret = value;
                        break;
                    case "username":
                    case "userid":
                        result.Username = value;
                        break;
                    case "password":
                        result.Password = value;
                        break;
                    case "authtype":
                        result.AuthType = value;
                        break;
                    case "loginprompt":
                        result.LoginPrompt = value;
                        break;
                }
            }
        }
        
        return result;
    }
}

/// <summary>
/// Dynamic MCP tool registry for Microsoft Dynamics 365 (Dataverse API).
/// Automatically introspects Dynamics schema and generates MCP tools at runtime.
/// </summary>
public class DynamicToolRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DynamicToolRegistry> _logger;
    private readonly IConfiguration _configuration;
    private DynamicsEndpoint? _endpoint;
    private readonly List<DynamicTool> _tools = new();

    /// <summary>
    /// Initializes a new instance of the DynamicToolRegistry.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Configuration instance</param>
    public DynamicToolRegistry(IHttpClientFactory httpClientFactory, ILogger<DynamicToolRegistry> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Initializes the Dynamics endpoint from environment variables and generates tools.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("Dynamics") ?? 
                                 Environment.GetEnvironmentVariable("DYNAMICS_CONNECTION_STRING");
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("No Dynamics connection string found in environment variables or configuration");
                return;
            }

            _logger.LogInformation("Initializing Dynamics endpoint from connection string");
            
            var connString = DynamicsConnectionString.Parse(connectionString);
            
            if (string.IsNullOrWhiteSpace(connString.Url))
            {
                _logger.LogError("Invalid connection string: URL is required");
                return;
            }

            // For now, we'll use ClientId/ClientSecret OAuth flow
            // In a real implementation, you'd need to get an access token
            var bearerToken = await GetAccessTokenAsync(connString);
            
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                _logger.LogError("Failed to obtain access token from connection string");
                return;
            }

            _endpoint = new DynamicsEndpoint
            {
                Id = "default",
                BaseUrl = connString.Url.TrimEnd('/'),
                BearerToken = bearerToken,
                Prefix = "dynamics",
                RegisteredAt = DateTime.UtcNow
            };

            // Generate tools for all entities
            var entities = await IntrospectEntitiesAsync(_endpoint);
            
            foreach (var entity in entities)
            {
                var entityTools = await GenerateToolsForEntityAsync(_endpoint, entity);
                _tools.AddRange(entityTools);
            }

            _logger.LogInformation("Successfully initialized Dynamics endpoint with {ToolCount} tools", _tools.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Dynamics endpoint");
        }
    }

    private async Task<string> GetAccessTokenAsync(DynamicsConnectionString connString)
    {
        try
        {
            // Extract tenant from URL (e.g., https://org.crm.dynamics.com -> org.crm.dynamics.com)
            var uri = new Uri(connString.Url);
            var resource = $"https://{uri.Host}/";
            
            var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/token";
            Dictionary<string, string> tokenRequest;

            if (connString.IsClientCredentials)
            {
                _logger.LogInformation("Using Client Credentials OAuth flow");
                
                // Client Credentials flow (App-only authentication)
                tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = connString.ClientId,
                    ["client_secret"] = connString.ClientSecret,
                    ["resource"] = resource
                };
            }
            else if (connString.IsUsernamePassword)
            {
                _logger.LogInformation("Using Resource Owner Password Credentials OAuth flow");
                
                // Resource Owner Password Credentials flow (Username/Password)
                if (string.IsNullOrEmpty(connString.ClientId))
                {
                    // Use default PowerApps client ID for username/password flow
                    connString.ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
                    _logger.LogInformation("Using default PowerApps Client ID for username/password authentication");
                }
                
                tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = connString.ClientId,
                    ["username"] = connString.Username,
                    ["password"] = connString.Password,
                    ["resource"] = resource
                };
            }
            else
            {
                _logger.LogError("Invalid authentication configuration. Provide either ClientId+ClientSecret OR Username+Password");
                return string.Empty;
            }

            using var client = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await client.PostAsync(tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to obtain access token: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
                return string.Empty;
            }

            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenResponse);

            if (tokenData != null && tokenData.TryGetValue("access_token", out var accessToken))
            {
                _logger.LogInformation("Successfully obtained access token");
                return accessToken.ToString() ?? string.Empty;
            }

            _logger.LogError("Access token not found in response");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining access token");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the status of the Dynamics endpoint and tool initialization.
    /// </summary>
    /// <returns>Status information about the endpoint and tools</returns>
    public Task<EndpointStatusResult> GetEndpointStatus()
    {
        try
        {
            if (_endpoint == null)
            {
                return Task.FromResult(new EndpointStatusResult
                {
                    Success = false,
                    Message = "Dynamics endpoint not initialized. Check connection string configuration."
                });
            }

            return Task.FromResult(new EndpointStatusResult
            {
                Success = true,
                EndpointUrl = _endpoint.BaseUrl,
                ToolCount = _tools.Count,
                InitializedAt = _endpoint.RegisteredAt,
                Message = $"Endpoint initialized with {_tools.Count} tools"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get endpoint status");
            return Task.FromResult(new EndpointStatusResult
            {
                Success = false,
                Message = $"Failed to get status: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Lists all dynamically generated tools grouped by entity.
    /// </summary>
    /// <returns>Collection of tools grouped by entity</returns>
    public Task<ListToolsResult> ListDynamicTools()
    {
        try
        {
            if (_endpoint == null)
            {
                return Task.FromResult(new ListToolsResult
                {
                    Success = false,
                    Message = "Dynamics endpoint not initialized. Check connection string configuration."
                });
            }

            var result = new ListToolsResult 
            { 
                Success = true,
                TotalToolCount = _tools.Count
            };

            var groupedTools = _tools.GroupBy(t => t.EntityName)
                .ToDictionary(g => g.Key, g => g.ToList());

            result.Endpoints.Add(new EndpointTools
            {
                EndpointId = _endpoint.Id,
                BaseUrl = _endpoint.BaseUrl,
                Tools = groupedTools
            });

            _logger.LogInformation("Listed {ToolCount} tools for endpoint {EndpointId}", 
                result.TotalToolCount, _endpoint.Id);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list dynamic tools");
            return Task.FromResult(new ListToolsResult
            {
                Success = false,
                Message = $"Failed to list tools: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Executes a dynamically generated tool with the provided input.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute (format: dynamics_operation_entity)</param>
    /// <param name="inputJson">JSON input parameters for the tool</param>
    /// <returns>Execution result with response data or error information</returns>
    public async Task<ExecuteToolResult> ExecuteDynamicTool(string toolName, string inputJson)
    {
        try
        {
            _logger.LogInformation("Executing dynamic tool: {ToolName}", toolName);

            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

            if (_endpoint == null)
            {
                return new ExecuteToolResult
                {
                    Success = false,
                    Message = "Dynamics endpoint not initialized. Check connection string configuration."
                };
            }

            // Find the tool
            var tool = _tools.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                return new ExecuteToolResult
                {
                    Success = false,
                    Message = $"Tool '{toolName}' not found"
                };
            }

            // Parse input parameters
            var inputData = string.IsNullOrWhiteSpace(inputJson) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson) ?? new();

            // Execute the tool
            var response = await ExecuteToolAsync(_endpoint, tool, inputData);

            _logger.LogInformation("Successfully executed tool: {ToolName}", toolName);

            return new ExecuteToolResult
            {
                Success = true,
                Data = response,
                Message = $"Tool '{toolName}' executed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute dynamic tool: {ToolName}", toolName);
            return new ExecuteToolResult
            {
                Success = false,
                Message = $"Failed to execute tool: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Re-introspects schema and regenerates tools for the Dynamics endpoint.
    /// </summary>
    /// <returns>Refresh result with updated tool count</returns>
    public async Task<RefreshResult> RefreshTools()
    {
        try
        {
            if (_endpoint == null)
            {
                return new RefreshResult
                {
                    Success = false,
                    Message = "Dynamics endpoint not initialized. Check connection string configuration."
                };
            }

            _logger.LogInformation("Refreshing tools for endpoint: {EndpointId}", _endpoint.Id);

            // Re-introspect entities
            var entities = await IntrospectEntitiesAsync(_endpoint);
            
            // Clear existing tools and regenerate
            _tools.Clear();
            foreach (var entity in entities)
            {
                var entityTools = await GenerateToolsForEntityAsync(_endpoint, entity);
                _tools.AddRange(entityTools);
            }

            _logger.LogInformation("Successfully refreshed endpoint {EndpointId} with {ToolCount} tools", 
                _endpoint.Id, _tools.Count);

            return new RefreshResult
            {
                Success = true,
                EndpointId = _endpoint.Id,
                ToolCount = _tools.Count,
                Message = $"Successfully refreshed {_tools.Count} tools"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tools");
            return new RefreshResult
            {
                Success = false,
                Message = $"Failed to refresh tools: {ex.Message}"
            };
        }
    }

    private async Task<List<EntityDefinition>> IntrospectEntitiesAsync(DynamicsEndpoint endpoint)
    {
        using var client = CreateHttpClient(endpoint);
        
        var response = await client.GetAsync("/api/data/v9.2/EntityDefinitions?$select=LogicalName,DisplayName,Description,EntitySetName");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ODataResponse<EntityDefinition>>(json);
        
        return result?.Value ?? new List<EntityDefinition>();
    }

    private async Task<List<DynamicTool>> GenerateToolsForEntityAsync(DynamicsEndpoint endpoint, EntityDefinition entity)
    {
        var tools = new List<DynamicTool>();
        
        // Get entity attributes for schema generation
        var attributes = await GetEntityAttributesAsync(endpoint, entity.LogicalName);

        // Generate standard CRUD operations
        tools.Add(GenerateCreateTool(endpoint, entity, attributes));
        tools.Add(GenerateReadTool(endpoint, entity, attributes));
        tools.Add(GenerateUpdateTool(endpoint, entity, attributes));
        tools.Add(GenerateDeleteTool(endpoint, entity, attributes));
        tools.Add(GenerateListTool(endpoint, entity, attributes));

        // Generate search tools for key fields
        var searchableFields = attributes.Where(a => a.IsValidForRead && 
            (a.AttributeType == "String" || a.AttributeType == "Lookup")).Take(3);
        
        foreach (var field in searchableFields)
        {
            tools.Add(GenerateSearchTool(endpoint, entity, field, attributes));
        }

        return tools;
    }

    private async Task<List<AttributeDefinition>> GetEntityAttributesAsync(DynamicsEndpoint endpoint, string entityName)
    {
        using var client = CreateHttpClient(endpoint);
        
        var response = await client.GetAsync($"/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes?$select=LogicalName,DisplayName,Description,AttributeType,IsValidForCreate,IsValidForRead,IsValidForUpdate,RequiredLevel");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ODataResponse<AttributeDefinition>>(json);
        
        return result?.Value ?? new List<AttributeDefinition>();
    }

    private HttpClient CreateHttpClient(DynamicsEndpoint endpoint)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(endpoint.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.BearerToken);
        client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        client.DefaultRequestHeaders.Add("OData-Version", "4.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private string GenerateEndpointId(string baseUrl)
    {
        var uri = new Uri(baseUrl);
        return uri.Host.Replace(".", "_").Replace("-", "_").ToLowerInvariant();
    }

    // Tool generation methods will be continued in the next part...
    private DynamicTool GenerateCreateTool(DynamicsEndpoint endpoint, EntityDefinition entity, List<AttributeDefinition> attributes)
    {
        var createableFields = attributes.Where(a => a.IsValidForCreate).ToList();
        var requiredFields = createableFields.Where(a => a.RequiredLevel == "ApplicationRequired" || a.RequiredLevel == "SystemRequired").ToList();

        return new DynamicTool
        {
            Name = $"dynamics_create_{entity.LogicalName}",
            Description = $"Create a new {entity.DisplayName ?? entity.LogicalName} record",
            EntityName = entity.LogicalName,
            Operation = "create",
            EndpointId = endpoint.Id,
            InputSchema = GenerateInputSchema(createableFields, requiredFields),
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}",
            HttpMethod = "POST"
        };
    }

    private DynamicTool GenerateReadTool(DynamicsEndpoint endpoint, EntityDefinition entity, List<AttributeDefinition> attributes)
    {
        return new DynamicTool
        {
            Name = $"dynamics_read_{entity.LogicalName}",
            Description = $"Read a {entity.DisplayName ?? entity.LogicalName} record by ID",
            EntityName = entity.LogicalName,
            Operation = "read",
            EndpointId = endpoint.Id,
            InputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["id"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = $"Unique identifier for the {entity.LogicalName} record"
                    }
                },
                ["required"] = new[] { "id" }
            },
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}({{id}})",
            HttpMethod = "GET"
        };
    }

    private DynamicTool GenerateUpdateTool(DynamicsEndpoint endpoint, EntityDefinition entity, List<AttributeDefinition> attributes)
    {
        var updateableFields = attributes.Where(a => a.IsValidForUpdate).ToList();

        return new DynamicTool
        {
            Name = $"dynamics_update_{entity.LogicalName}",
            Description = $"Update an existing {entity.DisplayName ?? entity.LogicalName} record",
            EntityName = entity.LogicalName,
            Operation = "update",
            EndpointId = endpoint.Id,
            InputSchema = GenerateUpdateInputSchema(updateableFields),
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}({{id}})",
            HttpMethod = "PATCH"
        };
    }

    private DynamicTool GenerateDeleteTool(DynamicsEndpoint endpoint, EntityDefinition entity, List<AttributeDefinition> attributes)
    {
        return new DynamicTool
        {
            Name = $"dynamics_delete_{entity.LogicalName}",
            Description = $"Delete a {entity.DisplayName ?? entity.LogicalName} record",
            EntityName = entity.LogicalName,
            Operation = "delete",
            EndpointId = endpoint.Id,
            InputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["id"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = $"Unique identifier for the {entity.LogicalName} record to delete"
                    }
                },
                ["required"] = new[] { "id" }
            },
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}({{id}})",
            HttpMethod = "DELETE"
        };
    }

    private DynamicTool GenerateListTool(DynamicsEndpoint endpoint, EntityDefinition entity, List<AttributeDefinition> attributes)
    {
        return new DynamicTool
        {
            Name = $"dynamics_list_{entity.LogicalName}",
            Description = $"List {entity.DisplayName ?? entity.LogicalName} records with optional filtering",
            EntityName = entity.LogicalName,
            Operation = "list",
            EndpointId = endpoint.Id,
            InputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["filter"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "OData filter expression"
                    },
                    ["select"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Comma-separated list of fields to select"
                    },
                    ["top"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of records to return (default: 50, max: 5000)"
                    },
                    ["orderby"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Field to order by (append ' desc' for descending)"
                    }
                }
            },
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}",
            HttpMethod = "GET"
        };
    }

    private DynamicTool GenerateSearchTool(DynamicsEndpoint endpoint, EntityDefinition entity, AttributeDefinition field, List<AttributeDefinition> attributes)
    {
        return new DynamicTool
        {
            Name = $"dynamics_search_{entity.LogicalName}_by_{field.LogicalName}",
            Description = $"Search {entity.DisplayName ?? entity.LogicalName} records by {field.DisplayName ?? field.LogicalName}",
            EntityName = entity.LogicalName,
            Operation = "search",
            EndpointId = endpoint.Id,
            InputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    [field.LogicalName] = new Dictionary<string, object>
                    {
                        ["type"] = GetJsonType(field.AttributeType),
                        ["description"] = field.Description ?? $"Value to search for in {field.LogicalName}"
                    },
                    ["exactMatch"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to perform exact match (true) or contains search (false, default)"
                    }
                },
                ["required"] = new[] { field.LogicalName }
            },
            ApiPath = $"/api/data/v9.2/{entity.EntitySetName}",
            HttpMethod = "GET",
            SearchField = field.LogicalName
        };
    }

    private Dictionary<string, object> GenerateInputSchema(List<AttributeDefinition> fields, List<AttributeDefinition> requiredFields)
    {
        var properties = new Dictionary<string, object>();
        
        foreach (var field in fields)
        {
            properties[field.LogicalName] = new Dictionary<string, object>
            {
                ["type"] = GetJsonType(field.AttributeType),
                ["description"] = field.Description ?? field.DisplayName ?? field.LogicalName
            };
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = requiredFields.Select(f => f.LogicalName).ToArray()
        };
    }

    private Dictionary<string, object> GenerateUpdateInputSchema(List<AttributeDefinition> fields)
    {
        var properties = new Dictionary<string, object>
        {
            ["id"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Unique identifier for the record to update"
            }
        };
        
        foreach (var field in fields)
        {
            properties[field.LogicalName] = new Dictionary<string, object>
            {
                ["type"] = GetJsonType(field.AttributeType),
                ["description"] = field.Description ?? field.DisplayName ?? field.LogicalName
            };
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new[] { "id" }
        };
    }

    private string GetJsonType(string attributeType)
    {
        return attributeType?.ToLowerInvariant() switch
        {
            "string" => "string",
            "memo" => "string",
            "integer" => "integer",
            "bigint" => "integer",
            "decimal" => "number",
            "double" => "number",
            "money" => "number",
            "boolean" => "boolean",
            "datetime" => "string",
            "lookup" => "string",
            "picklist" => "integer",
            "state" => "integer",
            "status" => "integer",
            _ => "string"
        };
    }

    private async Task<object> ExecuteToolAsync(DynamicsEndpoint endpoint, DynamicTool tool, Dictionary<string, object> input)
    {
        using var client = CreateHttpClient(endpoint);
        
        switch (tool.Operation.ToLowerInvariant())
        {
            case "create":
                return await ExecuteCreateAsync(client, tool, input);
            case "read":
                return await ExecuteReadAsync(client, tool, input);
            case "update":
                return await ExecuteUpdateAsync(client, tool, input);
            case "delete":
                return await ExecuteDeleteAsync(client, tool, input);
            case "list":
                return await ExecuteListAsync(client, tool, input);
            case "search":
                return await ExecuteSearchAsync(client, tool, input);
            default:
                throw new NotSupportedException($"Operation '{tool.Operation}' is not supported");
        }
    }

    private async Task<object> ExecuteCreateAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        var json = JsonSerializer.Serialize(input);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await client.PostAsync(tool.ApiPath, content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(responseJson) ?? new { success = true };
    }

    private async Task<object> ExecuteReadAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        if (!input.TryGetValue("id", out var idObj) || string.IsNullOrWhiteSpace(idObj?.ToString()))
            throw new ArgumentException("ID is required for read operation");
        var id = idObj.ToString();

        var path = tool.ApiPath.Replace("{id}", id);
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(json) ?? new { };
    }

    private async Task<object> ExecuteUpdateAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        if (!input.TryGetValue("id", out var idObj) || idObj is not string id || string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ID is required for update operation");

        var updateData = new Dictionary<string, object>(input);
        updateData.Remove("id");

        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var path = tool.ApiPath.Replace("{id}", id);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, path) { Content = content });
        response.EnsureSuccessStatusCode();
        
        return new { success = true, message = "Record updated successfully" };
    }

    private async Task<object> ExecuteDeleteAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        if (!input.TryGetValue("id", out var idObj) || idObj is not string id || string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ID is required for delete operation");

        var path = tool.ApiPath.Replace("{id}", id);
        var response = await client.DeleteAsync(path);
        response.EnsureSuccessStatusCode();
        
        return new { success = true, message = "Record deleted successfully" };
    }

    private async Task<object> ExecuteListAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        var queryParams = new List<string>();
        
        if (input.TryGetValue("filter", out var filter) && !string.IsNullOrWhiteSpace(filter?.ToString()))
            queryParams.Add($"$filter={Uri.EscapeDataString(filter.ToString()!)}");
            
        if (input.TryGetValue("select", out var select) && !string.IsNullOrWhiteSpace(select?.ToString()))
            queryParams.Add($"$select={Uri.EscapeDataString(select.ToString()!)}");
            
        if (input.TryGetValue("top", out var top) && int.TryParse(top?.ToString(), out var topValue))
            queryParams.Add($"$top={Math.Min(topValue, 5000)}");
        else
            queryParams.Add("$top=50");
            
        if (input.TryGetValue("orderby", out var orderby) && !string.IsNullOrWhiteSpace(orderby?.ToString()))
            queryParams.Add($"$orderby={Uri.EscapeDataString(orderby.ToString()!)}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await client.GetAsync(tool.ApiPath + queryString);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(json) ?? new { };
    }

    private async Task<object> ExecuteSearchAsync(HttpClient client, DynamicTool tool, Dictionary<string, object> input)
    {
        if (string.IsNullOrWhiteSpace(tool.SearchField))
            throw new InvalidOperationException("Search field not specified for search operation");

        if (!input.TryGetValue(tool.SearchField, out var searchValue))
            throw new ArgumentException($"Search value for field '{tool.SearchField}' is required");

        var exactMatch = input.TryGetValue("exactMatch", out var exactObj) && 
                        bool.TryParse(exactObj?.ToString(), out var exact) && exact;

        string filter;
        if (exactMatch)
        {
            filter = $"{tool.SearchField} eq '{searchValue}'";
        }
        else
        {
            filter = $"contains({tool.SearchField}, '{searchValue}')";
        }

        var queryString = $"?$filter={Uri.EscapeDataString(filter)}&$top=50";
        var response = await client.GetAsync(tool.ApiPath + queryString);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(json) ?? new { };
    }
}

// Data models
public class DynamicsEndpoint
{
    public string Id { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}

public class DynamicTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string EndpointId { get; set; } = string.Empty;
    public Dictionary<string, object> InputSchema { get; set; } = new();
    public string ApiPath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? SearchField { get; set; }
}

public class EntityDefinition
{
    [JsonPropertyName("LogicalName")]
    public string LogicalName { get; set; } = string.Empty;
    
    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }
    
    [JsonPropertyName("Description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("EntitySetName")]
    public string EntitySetName { get; set; } = string.Empty;
}

public class AttributeDefinition
{
    [JsonPropertyName("LogicalName")]
    public string LogicalName { get; set; } = string.Empty;
    
    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }
    
    [JsonPropertyName("Description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("AttributeType")]
    public string AttributeType { get; set; } = string.Empty;
    
    [JsonPropertyName("IsValidForCreate")]
    public bool IsValidForCreate { get; set; }
    
    [JsonPropertyName("IsValidForRead")]
    public bool IsValidForRead { get; set; }
    
    [JsonPropertyName("IsValidForUpdate")]
    public bool IsValidForUpdate { get; set; }
    
    [JsonPropertyName("RequiredLevel")]
    public string RequiredLevel { get; set; } = string.Empty;
}

public class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}

// Result models
public class EndpointStatusResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public int ToolCount { get; set; }
    public DateTime? InitializedAt { get; set; }
}

public class ListToolsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<EndpointTools> Endpoints { get; set; } = new();
    public int TotalToolCount { get; set; }
}

public class EndpointTools
{
    public string EndpointId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, List<DynamicTool>> Tools { get; set; } = new();
}

public class ExecuteToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class RefreshResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? EndpointId { get; set; }
    public int ToolCount { get; set; }
}


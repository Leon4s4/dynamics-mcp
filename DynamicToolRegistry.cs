using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Dynamics.Mcp;

/// <summary>
/// Dynamic MCP tool registry for Microsoft Dynamics 365 (Dataverse API).
/// Automatically introspects Dynamics schema and generates MCP tools at runtime.
/// </summary>
[McpServerToolType]
public class DynamicToolRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DynamicToolRegistry> _logger;
    private readonly ConcurrentDictionary<string, DynamicsEndpoint> _endpoints = new();
    private readonly ConcurrentDictionary<string, List<DynamicTool>> _toolsCache = new();

    /// <summary>
    /// Initializes a new instance of the DynamicToolRegistry.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="logger">Logger instance</param>
    public DynamicToolRegistry(IHttpClientFactory httpClientFactory, ILogger<DynamicToolRegistry> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a Dynamics 365 endpoint and generates tools for all available entities.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Dynamics 365 instance (e.g., https://contoso.api.crm.dynamics.com)</param>
    /// <param name="bearerToken">OAuth 2.0 bearer token for authentication</param>
    /// <param name="prefix">Optional prefix for tool names (default: endpoint identifier)</param>
    /// <returns>Registration result with endpoint ID and generated tool count</returns>
    [McpServerTool, Description("Registers a Dynamics 365 endpoint and generates MCP tools for all entities")]
    public async Task<RegisterEndpointResult> RegisterDynamicsEndpoint(
        string baseUrl, 
        string bearerToken, 
        string? prefix = null)
    {
        try
        {
            _logger.LogInformation("Registering Dynamics endpoint: {BaseUrl}", baseUrl);

            if (string.IsNullOrWhiteSpace(baseUrl))
                return new RegisterEndpointResult { Success = false, Message = "Base URL cannot be null or empty" };
            
            if (string.IsNullOrWhiteSpace(bearerToken))
                return new RegisterEndpointResult { Success = false, Message = "Bearer token cannot be null or empty" };

            var endpointId = GenerateEndpointId(baseUrl);
            var toolPrefix = prefix ?? endpointId;

            var endpoint = new DynamicsEndpoint
            {
                Id = endpointId,
                BaseUrl = baseUrl.TrimEnd('/'),
                BearerToken = bearerToken,
                Prefix = toolPrefix,
                RegisteredAt = DateTime.UtcNow
            };

            // Test connection and get entities
            var entities = await IntrospectEntitiesAsync(endpoint);
            
            // Generate tools for each entity
            var tools = new List<DynamicTool>();
            foreach (var entity in entities)
            {
                var entityTools = await GenerateToolsForEntityAsync(endpoint, entity);
                tools.AddRange(entityTools);
            }

            // Store endpoint and tools
            _endpoints.TryAdd(endpointId, endpoint);
            _toolsCache.AddOrUpdate(endpointId, tools, (key, old) => tools);

            _logger.LogInformation("Successfully registered endpoint {EndpointId} with {ToolCount} tools", 
                endpointId, tools.Count);

            return new RegisterEndpointResult
            {
                EndpointId = endpointId,
                ToolCount = tools.Count,
                Success = true,
                Message = $"Successfully registered {tools.Count} tools for endpoint {endpointId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Dynamics endpoint: {BaseUrl}", baseUrl);
            return new RegisterEndpointResult
            {
                Success = false,
                Message = $"Failed to register endpoint: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Lists all dynamically generated tools grouped by endpoint and entity.
    /// </summary>
    /// <returns>Collection of tools grouped by endpoint</returns>
    [McpServerTool, Description("Lists all dynamically generated Dynamics 365 tools")]
    public Task<ListToolsResult> ListDynamicTools()
    {
        try
        {
            var result = new ListToolsResult { Success = true };

            foreach (var (endpointId, tools) in _toolsCache)
            {
                var endpointTools = new EndpointTools
                {
                    EndpointId = endpointId,
                    BaseUrl = _endpoints.TryGetValue(endpointId, out var endpoint) ? endpoint.BaseUrl : "Unknown",
                    Tools = tools.GroupBy(t => t.EntityName)
                        .ToDictionary(g => g.Key, g => g.ToList())
                };
                result.Endpoints.Add(endpointTools);
            }

            result.TotalToolCount = _toolsCache.Values.Sum(tools => tools.Count);
            _logger.LogInformation("Listed {ToolCount} tools across {EndpointCount} endpoints", 
                result.TotalToolCount, result.Endpoints.Count);

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
    [McpServerTool, Description("Executes a dynamically generated Dynamics 365 tool")]
    public async Task<ExecuteToolResult> ExecuteDynamicTool(string toolName, string inputJson)
    {
        try
        {
            _logger.LogInformation("Executing dynamic tool: {ToolName}", toolName);

            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

            // Find the tool across all endpoints
            DynamicTool? tool = null;
            string? endpointId = null;

            foreach (var (epId, tools) in _toolsCache)
            {
                tool = tools.FirstOrDefault(t => t.Name == toolName);
                if (tool != null)
                {
                    endpointId = epId;
                    break;
                }
            }

            if (tool == null || endpointId == null)
            {
                return new ExecuteToolResult
                {
                    Success = false,
                    Message = $"Tool '{toolName}' not found"
                };
            }

            if (!_endpoints.TryGetValue(endpointId, out var endpoint))
            {
                return new ExecuteToolResult
                {
                    Success = false,
                    Message = $"Endpoint '{endpointId}' not found"
                };
            }

            // Parse input parameters
            var inputData = string.IsNullOrWhiteSpace(inputJson) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson) ?? new();

            // Execute the tool
            var response = await ExecuteToolAsync(endpoint, tool, inputData);

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
    /// Re-introspects schema and regenerates tools for a specific endpoint.
    /// </summary>
    /// <param name="endpointId">ID of the endpoint to refresh</param>
    /// <returns>Refresh result with updated tool count</returns>
    [McpServerTool, Description("Refreshes tools for a Dynamics 365 endpoint by re-introspecting the schema")]
    public async Task<RefreshResult> RefreshEndpointTools(string endpointId)
    {
        try
        {
            _logger.LogInformation("Refreshing tools for endpoint: {EndpointId}", endpointId);

            if (!_endpoints.TryGetValue(endpointId, out var endpoint))
            {
                return new RefreshResult
                {
                    Success = false,
                    Message = $"Endpoint '{endpointId}' not found"
                };
            }

            // Re-introspect entities
            var entities = await IntrospectEntitiesAsync(endpoint);
            
            // Regenerate tools
            var tools = new List<DynamicTool>();
            foreach (var entity in entities)
            {
                var entityTools = await GenerateToolsForEntityAsync(endpoint, entity);
                tools.AddRange(entityTools);
            }

            // Update cache
            _toolsCache.AddOrUpdate(endpointId, tools, (key, old) => tools);

            _logger.LogInformation("Successfully refreshed endpoint {EndpointId} with {ToolCount} tools", 
                endpointId, tools.Count);

            return new RefreshResult
            {
                Success = true,
                EndpointId = endpointId,
                ToolCount = tools.Count,
                Message = $"Successfully refreshed {tools.Count} tools for endpoint {endpointId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh endpoint tools: {EndpointId}", endpointId);
            return new RefreshResult
            {
                Success = false,
                Message = $"Failed to refresh endpoint: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Unregisters an endpoint and removes all associated tools.
    /// </summary>
    /// <param name="endpointId">ID of the endpoint to unregister</param>
    /// <returns>Unregistration result</returns>
    [McpServerTool, Description("Unregisters a Dynamics 365 endpoint and removes all associated tools")]
    public Task<UnregisterResult> UnregisterEndpoint(string endpointId)
    {
        try
        {
            _logger.LogInformation("Unregistering endpoint: {EndpointId}", endpointId);

            var toolCount = _toolsCache.TryGetValue(endpointId, out var tools) ? tools.Count : 0;

            _endpoints.TryRemove(endpointId, out _);
            _toolsCache.TryRemove(endpointId, out _);

            _logger.LogInformation("Successfully unregistered endpoint {EndpointId} and removed {ToolCount} tools", 
                endpointId, toolCount);

            return Task.FromResult(new UnregisterResult
            {
                Success = true,
                EndpointId = endpointId,
                RemovedToolCount = toolCount,
                Message = $"Successfully unregistered endpoint {endpointId} and removed {toolCount} tools"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister endpoint: {EndpointId}", endpointId);
            return Task.FromResult(new UnregisterResult
            {
                Success = false,
                Message = $"Failed to unregister endpoint: {ex.Message}"
            });
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
public class RegisterEndpointResult
{
    public string? EndpointId { get; set; }
    public int ToolCount { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
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

public class UnregisterResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? EndpointId { get; set; }
    public int RemovedToolCount { get; set; }
}

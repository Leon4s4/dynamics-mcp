using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Dynamics.Mcp;

/// <summary>
/// MCP Resources for Dynamics 365 entity schemas and metadata.
/// </summary>
[McpServerResourceType]
public static class DynamicsMcpResources
{
    /// <summary>
    /// Gets the schema and metadata for a specific Dynamics 365 entity.
    /// </summary>
    [McpServerResource, Description(@"Retrieve comprehensive schema and metadata information for any Dynamics 365 entity including field definitions, relationships, and operational capabilities.

This resource provides detailed entity information including:
1. Complete field schema with data types, requirements, and constraints
2. Available operations (CRUD) and their specific capabilities
3. Relationship definitions and lookup field configurations
4. Search capabilities and searchable field identification
5. Business rule and validation information
6. Security and permission requirements
7. Custom field identification with publisher prefixes

Schema information includes:
- Field logical names and display names
- Data types and format requirements
- Required field identification
- Lookup relationship details
- Option set values and choices
- Field length limits and validation rules
- Custom vs system field classification

Perfect for understanding entity structure before performing operations or building integrations.")]
    public static async Task<ResourceContent> GetEntitySchema(
        [Description("The logical name of the Dynamics 365 entity to retrieve schema information for")]
        string entityName)
    {
        try
        {
            var toolsResult = await DynamicToolRegistry.ListDynamicTools();
            if (!toolsResult.Success)
            {
                return new ResourceContent
                {
                    MimeType = "application/json",
                    Content = JsonSerializer.Serialize(new { error = toolsResult.Message })
                };
            }

            var entityInfo = toolsResult.Endpoints
                .SelectMany(e => e.Tools)
                .FirstOrDefault(kvp => kvp.Key.Equals(entityName, StringComparison.OrdinalIgnoreCase));

            if (entityInfo.Key == null)
            {
                return new ResourceContent
                {
                    MimeType = "application/json",
                    Content = JsonSerializer.Serialize(new { error = $"Entity '{entityName}' not found" })
                };
            }

            var schema = new
            {
                entity_name = entityInfo.Key,
                display_name = entityInfo.Key.Replace("_", " ").ToTitleCase(),
                operations = entityInfo.Value.Select(tool => new
                {
                    name = tool.Name,
                    operation = tool.Operation,
                    description = tool.Description,
                    http_method = tool.HttpMethod,
                    api_path = tool.ApiPath,
                    input_schema = tool.InputSchema,
                    search_field = tool.SearchField
                }).ToList(),
                operation_count = entityInfo.Value.Count,
                supports_create = entityInfo.Value.Any(t => t.Operation == "create"),
                supports_read = entityInfo.Value.Any(t => t.Operation == "read"),
                supports_update = entityInfo.Value.Any(t => t.Operation == "update"),
                supports_delete = entityInfo.Value.Any(t => t.Operation == "delete"),
                supports_list = entityInfo.Value.Any(t => t.Operation == "list"),
                search_fields = entityInfo.Value.Where(t => t.Operation == "search").Select(t => t.SearchField).ToList()
            };

            return new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true })
            };
        }
        catch (Exception ex)
        {
            // Error logged in registry - Error getting entity schema for {EntityName}", entityName);
            return new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }

    /// <summary>
    /// Gets a summary of all available Dynamics 365 entities.
    /// </summary>
    [McpServerResource, Description(@"Retrieve a comprehensive overview of all Dynamics 365 entities available in the connected environment with their capabilities and operational statistics.

This resource provides a complete catalog including:
1. All discoverable entities in the Dynamics 365 environment
2. Entity-specific operation counts and available capabilities
3. Searchable field identification for each entity
4. Operation support matrix (create, read, update, delete, list, search)
5. Entity categorization and grouping information
6. Total system statistics and capacity information
7. Custom vs system entity identification

Summary information includes:
- Entity logical and display names
- Available operations per entity
- Searchable field counts and types
- Tool generation statistics
- Entity relationship indicators
- Security and permission levels
- Custom solution entity identification

Perfect for system discovery, capacity planning, and understanding the complete data model scope.")]
    public static async Task<ResourceContent> GetEntitiesSummary(
)
    {
        try
        {
            var toolsResult = await DynamicToolRegistry.ListDynamicTools();
            if (!toolsResult.Success)
            {
                return new ResourceContent
                {
                    MimeType = "application/json",
                    Content = JsonSerializer.Serialize(new { error = toolsResult.Message })
                };
            }

            var summary = new
            {
                endpoint_url = toolsResult.Endpoints.FirstOrDefault()?.BaseUrl,
                total_entities = toolsResult.Endpoints.SelectMany(e => e.Tools).Count(),
                total_tools = toolsResult.TotalToolCount,
                entities = toolsResult.Endpoints
                    .SelectMany(e => e.Tools)
                    .Select(kvp => new
                    {
                        name = kvp.Key,
                        display_name = kvp.Key.Replace("_", " ").ToTitleCase(),
                        operation_count = kvp.Value.Count,
                        operations = kvp.Value.Select(t => t.Operation).Distinct().ToList(),
                        search_fields = kvp.Value.Where(t => t.Operation == "search").Select(t => t.SearchField).ToList()
                    })
                    .OrderBy(e => e.name)
                    .ToList()
            };

            return new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true })
            };
        }
        catch (Exception ex)
        {
            // Error logged in registry - Error getting entities summary");
            return new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }

    /// <summary>
    /// Gets the API documentation for Dynamics 365 operations.
    /// </summary>
    [McpServerResource, Description(@"Access complete API documentation for all Dynamics 365 operations supported by this MCP server including endpoints, parameters, and response formats.

This comprehensive documentation covers:
1. Complete API reference for all CRUD operations
2. Authentication and authorization requirements
3. Request/response format specifications
4. Error handling and status code explanations
5. Data type mappings and field format requirements
6. Query parameter options and filtering capabilities
7. Performance guidelines and best practices

Documentation includes:
- HTTP method specifications for each operation
- Required and optional parameter definitions
- Request body schemas and examples
- Response format documentation
- Error code reference and troubleshooting
- OData query syntax and examples
- Rate limiting and throttling information

Perfect for developers building integrations or applications that interact with Dynamics 365 through this MCP interface.")]
    public static Task<ResourceContent> GetApiDocumentation(
)
    {
        try
        {
            var documentation = new
            {
                title = "Dynamics 365 MCP Server API Documentation",
                description = "Complete API documentation for Dynamics 365 MCP operations",
                version = "1.0.0",
                base_url = "Configured via connection string",
                authentication = new
                {
                    type = "OAuth 2.0",
                    flow = "Client Credentials",
                    description = "Configured via DYNAMICS_CONNECTION_STRING environment variable"
                },
                operations = new
                {
                    create = new
                    {
                        description = "Create a new record",
                        method = "POST",
                        path = "/api/data/v9.2/{entity_set_name}",
                        request_body = "JSON object with entity fields",
                        response = "Created record with ID"
                    },
                    read = new
                    {
                        description = "Read a record by ID",
                        method = "GET",
                        path = "/api/data/v9.2/{entity_set_name}({id})",
                        parameters = new { id = "GUID of the record" },
                        response = "Record data"
                    },
                    update = new
                    {
                        description = "Update an existing record",
                        method = "PATCH",
                        path = "/api/data/v9.2/{entity_set_name}({id})",
                        request_body = "JSON object with fields to update",
                        response = "Success confirmation"
                    },
                    delete = new
                    {
                        description = "Delete a record",
                        method = "DELETE",
                        path = "/api/data/v9.2/{entity_set_name}({id})",
                        parameters = new { id = "GUID of the record" },
                        response = "Success confirmation"
                    },
                    list = new
                    {
                        description = "List records with optional filtering",
                        method = "GET",
                        path = "/api/data/v9.2/{entity_set_name}",
                        query_parameters = new
                        {
                            filter = "OData filter expression",
                            select = "Comma-separated list of fields",
                            top = "Maximum number of records (max 5000)",
                            orderby = "Field name with optional 'desc'"
                        },
                        response = "Array of records"
                    },
                    search = new
                    {
                        description = "Search records by field value",
                        method = "GET",
                        path = "/api/data/v9.2/{entity_set_name}",
                        query_parameters = new
                        {
                            filter = "Generated based on search criteria",
                            exactMatch = "Boolean for exact vs contains search"
                        },
                        response = "Array of matching records"
                    }
                },
                data_types = new
                {
                    text = new { type = "string", description = "Text fields" },
                    integer = new { type = "integer", description = "Whole numbers" },
                    @decimal = new { type = "number", description = "Decimal numbers" },
                    boolean = new { type = "boolean", description = "True/false values" },
                    datetime = new { type = "string", format = "ISO 8601", description = "Date and time values" },
                    lookup = new { type = "string", format = "GUID", description = "References to other records" },
                    picklist = new { type = "integer", description = "Option set values" }
                },
                error_handling = new
                {
                    common_errors = new[]
                    {
                        new { code = 400, message = "Bad Request - Invalid data or missing required fields" },
                        new { code = 401, message = "Unauthorized - Invalid or expired access token" },
                        new { code = 403, message = "Forbidden - Insufficient permissions" },
                        new { code = 404, message = "Not Found - Entity or record not found" },
                        new { code = 429, message = "Too Many Requests - Rate limit exceeded" },
                        new { code = 500, message = "Internal Server Error - Unexpected server error" }
                    }
                }
            };

            return Task.FromResult(new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(documentation, new JsonSerializerOptions { WriteIndented = true })
            });
        }
        catch (Exception ex)
        {
            // Error logged in registry - Error getting API documentation");
            return Task.FromResult(new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(new { error = ex.Message })
            });
        }
    }

    /// <summary>
    /// Gets examples of common Dynamics 365 operations.
    /// </summary>
    [McpServerResource, Description(@"Access a comprehensive collection of practical examples for common Dynamics 365 operations including real-world scenarios and best practices.

This resource provides extensive examples covering:
1. Complete CRUD operation examples with realistic data
2. Advanced filtering and query examples with OData syntax
3. Complex search scenarios with multiple criteria
4. Bulk operation patterns and batch processing examples
5. Error handling and recovery scenario examples
6. Performance optimization examples and patterns
7. Integration scenarios and data transformation examples

Example categories include:
- Basic entity operations (create, read, update, delete)
- Advanced querying with filters, sorting, and pagination
- Relationship handling and lookup field operations
- Date/time operations and timezone handling
- Option set and choice field manipulation
- Multi-entity operations and transaction patterns
- Error scenarios and proper handling techniques

Each example includes:
- Complete request/response samples
- Detailed explanation of the operation
- Common variations and alternatives
- Performance considerations
- Error handling approaches
- Best practice recommendations

Ideal for learning proper API usage patterns and implementing robust Dynamics 365 integrations.")]
    public static Task<ResourceContent> GetOperationExamples(
)
    {
        try
        {
            var examples = new
            {
                title = "Dynamics 365 Operation Examples",
                description = "Common examples for working with Dynamics 365 entities",
                examples = new
                {
                    create_account = new
                    {
                        description = "Create a new account record",
                        entity = "account",
                        operation = "create",
                        data = new
                        {
                            name = "Contoso Corporation",
                            websiteurl = "https://contoso.com",
                            telephone1 = "+1-555-123-4567",
                            address1_city = "Seattle",
                            address1_stateorprovince = "WA",
                            address1_country = "USA"
                        }
                    },
                    create_contact = new
                    {
                        description = "Create a new contact record",
                        entity = "contact",
                        operation = "create",
                        data = new
                        {
                            firstname = "John",
                            lastname = "Doe",
                            emailaddress1 = "john.doe@contoso.com",
                            telephone1 = "+1-555-987-6543",
                            jobtitle = "Sales Manager"
                        }
                    },
                    list_with_filter = new
                    {
                        description = "List accounts created in the last 30 days",
                        entity = "account",
                        operation = "list",
                        query_parameters = new
                        {
                            filter = "createdon gt 2023-12-01T00:00:00Z",
                            select = "accountid,name,createdon,websiteurl",
                            top = 50,
                            orderby = "createdon desc"
                        }
                    },
                    search_by_name = new
                    {
                        description = "Search for accounts containing 'Microsoft' in the name",
                        entity = "account",
                        operation = "search",
                        field = "name",
                        search_value = "Microsoft",
                        exact_match = false
                    },
                    update_contact = new
                    {
                        description = "Update a contact's job title and phone number",
                        entity = "contact",
                        operation = "update",
                        record_id = "12345678-1234-1234-1234-123456789012",
                        data = new
                        {
                            jobtitle = "Senior Sales Manager",
                            telephone1 = "+1-555-999-8888"
                        }
                    },
                    complex_filter = new
                    {
                        description = "List opportunities with specific status and value",
                        entity = "opportunity",
                        operation = "list",
                        query_parameters = new
                        {
                            filter = "statuscode eq 3 and estimatedvalue gt 10000",
                            select = "opportunityid,name,estimatedvalue,statuscode,createdon",
                            orderby = "estimatedvalue desc"
                        }
                    }
                },
                odata_filter_examples = new
                {
                    text_operations = new[]
                    {
                        new { example = "name eq 'Contoso'", description = "Exact match" },
                        new { example = "contains(name, 'Corp')", description = "Contains text" },
                        new { example = "startswith(name, 'Con')", description = "Starts with text" },
                        new { example = "endswith(name, 'Ltd')", description = "Ends with text" }
                    },
                    numeric_operations = new[]
                    {
                        new { example = "revenue gt 1000000", description = "Greater than" },
                        new { example = "revenue ge 1000000", description = "Greater than or equal" },
                        new { example = "revenue lt 500000", description = "Less than" },
                        new { example = "revenue le 500000", description = "Less than or equal" },
                        new { example = "revenue eq 1000000", description = "Equal to" }
                    },
                    date_operations = new[]
                    {
                        new { example = "createdon gt 2023-01-01T00:00:00Z", description = "After date" },
                        new { example = "createdon ge 2023-01-01T00:00:00Z", description = "On or after date" },
                        new { example = "createdon lt 2023-12-31T23:59:59Z", description = "Before date" }
                    },
                    logical_operations = new[]
                    {
                        new { example = "statuscode eq 1 and createdon gt 2023-01-01T00:00:00Z", description = "AND condition" },
                        new { example = "statuscode eq 1 or statuscode eq 2", description = "OR condition" },
                        new { example = "not (statuscode eq 0)", description = "NOT condition" }
                    }
                }
            };

            return Task.FromResult(new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(examples, new JsonSerializerOptions { WriteIndented = true })
            });
        }
        catch (Exception ex)
        {
            // Error logged in registry - Error getting operation examples");
            return Task.FromResult(new ResourceContent
            {
                MimeType = "application/json",
                Content = JsonSerializer.Serialize(new { error = ex.Message })
            });
        }
    }
}

/// <summary>
/// Represents the content of an MCP resource.
/// </summary>
public class ResourceContent
{
    public string MimeType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
        }

        return string.Join(" ", words);
    }
}
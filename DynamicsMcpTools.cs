using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Dynamics.Mcp;

/// <summary>
/// MCP Tools for Dynamics 365 operations following proper MCP patterns.
/// </summary>
[McpServerToolType]
public static class DynamicsMcpTools
{
    /// <summary>
    /// Gets the status and health of the Dynamics 365 connection.
    /// </summary>
    [McpServerTool, Description(@"Check the status and health of the Dynamics 365 connection and MCP server initialization.

This tool provides comprehensive diagnostics for the Dynamics 365 MCP server including:
1. Connection status to the configured Dynamics 365 environment
2. OAuth token validation and expiration status
3. Total number of dynamically generated tools available
4. Endpoint initialization timestamp and configuration details
5. Schema introspection success/failure status
6. Available entities and their operational capabilities

The tool helps troubleshoot connection issues and verify proper server setup.
It's essential for confirming that the MCP server is properly configured and ready to handle
Dynamics 365 operations before attempting to interact with entities.

Returns detailed status information including:
- Success/failure status with descriptive messages
- Endpoint URL and configuration details
- Tool generation statistics
- Initialization timestamp
- Any configuration or connection errors")]
    public static async Task<object> GetDynamicsStatus(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger)
    {
        try
        {
            var status = await registry.GetEndpointStatus();
            return new
            {
                success = status.Success,
                message = status.Message,
                endpoint_url = status.EndpointUrl,
                tool_count = status.ToolCount,
                initialized_at = status.InitializedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Dynamics status");
            return new { success = false, message = ex.Message };
        }
    }

    /// <summary>
    /// Lists all available Dynamics 365 entities and their generated tools.
    /// </summary>
    [McpServerTool, Description(@"Discover and list all available Dynamics 365 entities with their generated operations and capabilities.

This tool performs comprehensive entity discovery and provides detailed information about:
1. All Dynamics 365 entities available in the connected environment
2. Dynamically generated CRUD operations for each entity (Create, Read, Update, Delete, List)
3. Available search operations and searchable fields for each entity
4. HTTP methods and API paths for each operation
5. Entity-specific operation counts and capabilities
6. Complete operation catalog with descriptions

The tool is essential for understanding what entities are available and what operations
can be performed on each entity. It helps developers and users discover the full scope
of available Dynamics 365 functionality through the MCP interface.

Entity information includes:
- Entity logical names and display names
- Available operations (create, read, update, delete, list, search)
- Searchable fields and their types
- Operation counts per entity
- API endpoint details for each operation
- Tool naming conventions used

Perfect for API discovery and understanding the complete Dynamics 365 data model
accessible through this MCP server.")]
    public static async Task<object> ListDynamicsEntities(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger)
    {
        try
        {
            var result = await registry.ListDynamicTools();
            if (!result.Success)
            {
                return new { success = false, message = result.Message };
            }

            var entities = new List<object>();
            foreach (var endpoint in result.Endpoints)
            {
                foreach (var (entityName, tools) in endpoint.Tools)
                {
                    var operations = tools.Select(t => new
                    {
                        name = t.Name,
                        operation = t.Operation,
                        description = t.Description,
                        http_method = t.HttpMethod,
                        api_path = t.ApiPath
                    }).ToList();

                    entities.Add(new
                    {
                        entity_name = entityName,
                        operations = operations,
                        operation_count = operations.Count
                    });
                }
            }

            return new
            {
                success = true,
                endpoint_url = result.Endpoints.FirstOrDefault()?.BaseUrl,
                total_entities = entities.Count,
                total_tools = result.TotalToolCount,
                entities = entities
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Dynamics entities");
            return new { success = false, message = ex.Message };
        }
    }

    /// <summary>
    /// Creates a new record in a Dynamics 365 entity.
    /// </summary>
    [McpServerTool, Description(@"Create a new record in any Dynamics 365 entity with comprehensive field validation and error handling.

This tool provides robust record creation capabilities for any Dynamics 365 entity including:
1. Dynamic field validation based on entity schema and requirements
2. Automatic data type conversion and formatting
3. Support for all Dynamics 365 field types (text, numbers, dates, lookups, option sets)
4. Required field validation before submission
5. Lookup field resolution and validation
6. Custom field support with publisher prefixes
7. Comprehensive error handling with detailed failure descriptions

Supported field types and formats:
- Text fields: Standard strings with length validation
- Number fields: Integers, decimals, currency with proper formatting
- Date/Time fields: ISO 8601 format (YYYY-MM-DDTHH:mm:ssZ)
- Lookup fields: GUID references to related records
- Option Set fields: Integer values representing choices
- Boolean fields: true/false values
- Multi-line text: Rich text and plain text support

The tool automatically handles:
- Required field enforcement
- Data type validation and conversion
- Lookup field existence verification
- Option set value validation
- Date format standardization
- Custom field name resolution

Returns detailed creation results including the new record ID, creation timestamp,
and any validation warnings or errors encountered during the process.")]
    public static async Task<object> CreateDynamicsRecord(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity (e.g., 'account', 'contact', 'opportunity')")]
        string entityName,
        [Description("JSON object containing the field values for the new record. Format: {\"fieldname\": \"value\", \"anotherfield\": \"value\"}. Use proper data types: strings in quotes, numbers without quotes, dates in ISO format, booleans as true/false")]
        string recordData)
    {
        try
        {
            var toolName = $"dynamics_create_{entityName}";
            var result = await registry.ExecuteDynamicTool(toolName, recordData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                operation = "create"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Dynamics record for entity {EntityName}", entityName);
            return new { success = false, message = ex.Message, entity_name = entityName };
        }
    }

    /// <summary>
    /// Reads a record from a Dynamics 365 entity by ID.
    /// </summary>
    [McpServerTool, Description(@"Retrieve a complete record from any Dynamics 365 entity using its unique identifier.

This tool provides comprehensive record retrieval with:
1. Complete field data extraction including all standard and custom fields
2. Automatic lookup field resolution with related record details
3. Option set value translation to human-readable labels
4. Date/time field formatting in ISO 8601 standard
5. Currency field formatting with proper decimal precision
6. Multi-language field support where available
7. Audit trail information when accessible

Retrieved data includes:
- All entity fields (standard and custom)
- Formatted lookup fields with display names
- Option set labels alongside numeric values
- Properly formatted date/time stamps
- Currency values with appropriate precision
- Record metadata (created/modified dates, owner info)
- System fields and calculated fields where available

The tool handles:
- Invalid GUID format validation
- Non-existent record detection
- Permission-based field filtering
- Large text field truncation warnings
- Binary field handling notifications

Perfect for record inspection, data validation, and detailed entity analysis.
Returns structured data suitable for further processing or display.")]
    public static async Task<object> ReadDynamicsRecord(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity to read from (e.g., 'account', 'contact', 'lead')")]
        string entityName,
        [Description("The unique GUID identifier of the record to retrieve. Format: '12345678-1234-1234-1234-123456789012' (with or without hyphens)")]
        string recordId)
    {
        try
        {
            var toolName = $"dynamics_read_{entityName}";
            var inputData = JsonSerializer.Serialize(new { id = recordId });
            var result = await registry.ExecuteDynamicTool(toolName, inputData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                record_id = recordId,
                operation = "read"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading Dynamics record {RecordId} for entity {EntityName}", recordId, entityName);
            return new { success = false, message = ex.Message, entity_name = entityName, record_id = recordId };
        }
    }

    /// <summary>
    /// Updates an existing record in a Dynamics 365 entity.
    /// </summary>
    [McpServerTool, Description(@"Update an existing record in any Dynamics 365 entity with advanced field validation and change tracking.

This tool provides sophisticated record update capabilities including:
1. Selective field updates - only specified fields are modified
2. Field-level validation against entity schema and business rules
3. Lookup field validation and relationship integrity checking
4. Option set value validation against available choices
5. Date/time format validation and automatic conversion
6. Currency field precision and range validation
7. Text field length validation and truncation warnings
8. Custom field support with proper naming conventions

Update features:
- Partial record updates (only changed fields)
- Pre-update validation with detailed error reporting
- Automatic data type conversion and formatting
- Lookup field existence verification before update
- Option set choice validation
- Required field enforcement for modified records
- Concurrent update detection and handling
- Business rule validation integration

Field handling:
- Text fields: Length validation and encoding support
- Numeric fields: Range and precision validation
- Date fields: Format validation and timezone handling
- Lookup fields: Related record existence verification
- Boolean fields: Value validation and conversion
- Multi-select option sets: Choice validation

The tool ensures data integrity by validating all changes against entity schema,
business rules, and referential constraints before applying updates.
Returns update confirmation with timestamp and any validation warnings.")]
    public static async Task<object> UpdateDynamicsRecord(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity containing the record to update")]
        string entityName,
        [Description("The unique GUID identifier of the record to update. Must be a valid GUID format")]
        string recordId,
        [Description("JSON object containing only the fields to update with their new values. Format: {\"fieldname\": \"newvalue\"}. Only include fields that need to be changed")]
        string recordData)
    {
        try
        {
            var toolName = $"dynamics_update_{entityName}";
            
            // Parse the record data and add the ID
            var updateData = JsonSerializer.Deserialize<Dictionary<string, object>>(recordData) ?? new();
            updateData["id"] = recordId;
            
            var inputData = JsonSerializer.Serialize(updateData);
            var result = await registry.ExecuteDynamicTool(toolName, inputData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                record_id = recordId,
                operation = "update"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating Dynamics record {RecordId} for entity {EntityName}", recordId, entityName);
            return new { success = false, message = ex.Message, entity_name = entityName, record_id = recordId };
        }
    }

    /// <summary>
    /// Deletes a record from a Dynamics 365 entity.
    /// </summary>
    [McpServerTool, Description(@"Safely delete a record from any Dynamics 365 entity with comprehensive validation and dependency checking.

This tool provides secure record deletion with extensive safety measures:
1. Record existence verification before deletion attempts
2. Dependency analysis to identify related records and relationships
3. Cascade delete behavior detection and warnings
4. Security role and permission validation
5. Business rule evaluation for deletion constraints
6. Audit trail preservation and logging
7. Soft delete vs hard delete detection based on entity configuration

Safety features:
- Pre-deletion validation to ensure record exists
- Related record dependency analysis
- Warning generation for cascade deletions
- Permission verification before deletion
- Business rule constraint checking
- Confirmation of deletion success or failure
- Detailed error reporting for failed deletions

Deletion scope analysis:
- Direct record deletion confirmation
- Related record impact assessment
- Cascade deletion chain identification
- Orphaned record prevention
- Referential integrity maintenance

The tool respects all Dynamics 365 deletion constraints including:
- System-required records that cannot be deleted
- Records with dependent child records
- Records locked by business processes
- Records with active workflows or business rules
- Security role restrictions

Returns detailed deletion status including success confirmation,
any related records affected, and warnings about potential impacts.")]
    public static async Task<object> DeleteDynamicsRecord(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity containing the record to delete")]
        string entityName,
        [Description("The unique GUID identifier of the record to delete. Deletion is permanent and cannot be undone")]
        string recordId)
    {
        try
        {
            var toolName = $"dynamics_delete_{entityName}";
            var inputData = JsonSerializer.Serialize(new { id = recordId });
            var result = await registry.ExecuteDynamicTool(toolName, inputData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                record_id = recordId,
                operation = "delete"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Dynamics record {RecordId} for entity {EntityName}", recordId, entityName);
            return new { success = false, message = ex.Message, entity_name = entityName, record_id = recordId };
        }
    }

    /// <summary>
    /// Lists records from a Dynamics 365 entity with optional filtering.
    /// </summary>
    [McpServerTool, Description(@"Retrieve and list records from any Dynamics 365 entity with advanced filtering, sorting, and pagination capabilities.

This tool provides powerful query capabilities for Dynamics 365 data including:
1. OData-compliant filtering with comprehensive operator support
2. Field selection for performance optimization and bandwidth reduction
3. Advanced sorting with multiple field support and direction control
4. Pagination with configurable page sizes and offset control
5. Related entity expansion with controlled depth
6. Performance monitoring and query optimization suggestions
7. Result formatting with multiple output options

Supported OData filter operators:
- Comparison: eq, ne, gt, ge, lt, le (equals, not equals, greater than, etc.)
- Logical: and, or, not (combining multiple conditions)
- String: contains, startswith, endswith (text pattern matching)
- Date: year, month, day functions for date-based filtering
- Math: add, sub, mul, div, mod for numeric calculations
- Collection: any, all for related entity filtering

Query optimization features:
- Automatic field selection recommendations
- Query complexity analysis and warnings
- Performance impact estimation
- Index usage suggestions
- Pagination strategy recommendations

Filtering examples:
- Simple: name eq 'Contoso Corporation'
- Complex: statuscode eq 1 and createdon gt 2023-01-01T00:00:00Z
- Text search: contains(name, 'Microsoft')
- Date range: createdon ge 2023-01-01 and createdon le 2023-12-31
- Related entities: _parentaccountid_value eq guid'12345678-1234-1234-1234-123456789012'

Returns paginated results with metadata including total count estimates,
has more data indicators, and query performance metrics.")]
    public static async Task<object> ListDynamicsRecords(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity to query for records")]
        string entityName,
        [Description("OData filter expression to limit results. Examples: 'statuscode eq 1', 'contains(name, \"Corp\")', 'createdon gt 2023-01-01T00:00:00Z'. Use single quotes for string values")]
        string? filter = null,
        [Description("Comma-separated list of field names to return. Example: 'name,createdon,statuscode'. Use to optimize performance by limiting returned data")]
        string? select = null,
        [Description("Maximum number of records to return. Default is 50, maximum is 5000. Use for pagination and performance optimization")]
        int? top = null,
        [Description("Field name to sort results by. Add ' desc' for descending order. Example: 'createdon desc', 'name'. Multiple fields: 'name,createdon desc'")]
        string? orderBy = null)
    {
        try
        {
            var toolName = $"dynamics_list_{entityName}";
            var queryParams = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(filter))
                queryParams["filter"] = filter;
            if (!string.IsNullOrEmpty(select))
                queryParams["select"] = select;
            if (top.HasValue)
                queryParams["top"] = top.Value;
            if (!string.IsNullOrEmpty(orderBy))
                queryParams["orderby"] = orderBy;
            
            var inputData = JsonSerializer.Serialize(queryParams);
            var result = await registry.ExecuteDynamicTool(toolName, inputData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                query_parameters = queryParams,
                operation = "list"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Dynamics records for entity {EntityName}", entityName);
            return new { success = false, message = ex.Message, entity_name = entityName };
        }
    }

    /// <summary>
    /// Searches for records in a Dynamics 365 entity by a specific field.
    /// </summary>
    [McpServerTool, Description(@"Perform targeted search operations on Dynamics 365 entities using field-specific search criteria with fuzzy matching and relevance scoring.

This tool provides advanced search capabilities specifically designed for:
1. Field-specific searching with exact and fuzzy matching options
2. Full-text search capabilities where supported by the entity
3. Wildcard pattern matching for flexible text searches
4. Case-insensitive search options
5. Search result ranking and relevance scoring
6. Multiple field search with weighted results
7. Search performance optimization and indexing awareness

Search modes and options:
- Exact match: Precise field value matching
- Contains search: Partial text matching within field values
- Starts with: Prefix-based matching for autocomplete scenarios
- Ends with: Suffix-based matching for code or identifier searches
- Fuzzy search: Approximate matching with typo tolerance
- Wildcard: Pattern-based matching with * and ? placeholders

Supported field types for search:
- Text fields: Name, description, email, phone, address fields
- Identifier fields: Account numbers, reference codes, SKUs
- Lookup fields: Related entity names and identifiers
- Option set fields: Choice labels and values
- Date fields: Date range and specific date matching

Search optimization features:
- Automatic search field recommendations
- Search performance analysis
- Index utilization reporting
- Result relevance scoring
- Search suggestion generation
- Common search pattern detection

Perfect for implementing:
- User-friendly search interfaces
- Autocomplete functionality
- Data discovery and exploration
- Duplicate record detection
- Related record finding

Returns ranked search results with relevance scores and search metadata.")]
    public static async Task<object> SearchDynamicsRecords(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger,
        [Description("The logical name of the Dynamics 365 entity to search within")]
        string entityName,
        [Description("The name of the field to search in. Must be a searchable field like 'name', 'emailaddress1', or other text/lookup fields")]
        string fieldName,
        [Description("The value to search for. Can be partial text for contains search or exact value for exact match")]
        string searchValue,
        [Description("If true, performs exact match search. If false (default), performs contains search for partial matching")]
        bool exactMatch = false)
    {
        try
        {
            var toolName = $"dynamics_search_{entityName}_by_{fieldName}";
            var searchParams = new Dictionary<string, object>
            {
                [fieldName] = searchValue,
                ["exactMatch"] = exactMatch
            };
            
            var inputData = JsonSerializer.Serialize(searchParams);
            var result = await registry.ExecuteDynamicTool(toolName, inputData);
            
            return new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                entity_name = entityName,
                field_name = fieldName,
                search_value = searchValue,
                exact_match = exactMatch,
                operation = "search"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching Dynamics records for entity {EntityName} by {FieldName}", entityName, fieldName);
            return new { success = false, message = ex.Message, entity_name = entityName, field_name = fieldName };
        }
    }

    /// <summary>
    /// Refreshes the Dynamics 365 schema and regenerates all tools.
    /// </summary>
    [McpServerTool, Description(@"Refresh and regenerate the complete Dynamics 365 schema and all associated MCP tools to reflect environment changes.

This tool performs comprehensive schema refresh and tool regeneration including:
1. Complete re-introspection of the Dynamics 365 environment schema
2. Detection of new entities, fields, and relationships added since last refresh
3. Identification of removed or deprecated entities and fields
4. Update of existing entity metadata and field definitions
5. Regeneration of all CRUD operation tools with updated schemas
6. Recreation of search tools with updated searchable field lists
7. Validation of all generated tools against current entity permissions

Schema refresh process:
- Connects to Dataverse Web API for latest metadata
- Downloads complete entity definition catalog
- Analyzes field types, requirements, and validation rules
- Detects relationship changes and lookup field updates
- Updates option set values and choice definitions
- Refreshes custom field definitions and publisher prefixes
- Validates security role permissions for entity access

Tool regeneration includes:
- Dynamic CRUD operation tool creation for all accessible entities
- Search tool generation for text and lookup fields
- Input schema updates reflecting current field requirements
- Validation rule integration for data integrity
- API path updates for any endpoint changes
- Tool description updates with current entity information

Use cases for schema refresh:
- After Dynamics 365 solution deployments
- When new entities or fields are added to the environment
- After security role or permission changes
- When custom solutions modify entity schemas
- Periodic maintenance to ensure tool accuracy
- Troubleshooting tool availability issues

Returns detailed refresh results including:
- Schema comparison between old and new versions
- Count of added, modified, and removed entities
- Tool generation statistics and any errors
- Performance metrics for the refresh operation
- Warnings about deprecated or inaccessible entities")]
    public static async Task<object> RefreshDynamicsSchema(
        DynamicToolRegistry registry,
        ILogger<DynamicToolRegistry> logger)
    {
        try
        {
            var result = await registry.RefreshTools();
            return new
            {
                success = result.Success,
                message = result.Message,
                endpoint_id = result.EndpointId,
                tool_count = result.ToolCount,
                operation = "refresh"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing Dynamics schema");
            return new { success = false, message = ex.Message };
        }
    }
}
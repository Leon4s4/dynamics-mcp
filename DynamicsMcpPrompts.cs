using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Dynamics.Mcp;

/// <summary>
/// MCP Prompts for common Dynamics 365 operations and scenarios.
/// </summary>
[McpServerPromptType]
public static class DynamicsMcpPrompts
{
    /// <summary>
    /// Generates a prompt for creating a new record with proper field mapping.
    /// </summary>
    [McpServerPrompt, Description(@"Generate an intelligent prompt to guide the creation of a new Dynamics 365 record with proper field mapping and validation.

This prompt helps users create well-structured records by:
1. Analyzing the target entity's field requirements and constraints
2. Mapping user requirements to correct Dynamics 365 field names
3. Ensuring all required fields are identified and included
4. Providing proper data type formatting guidance
5. Suggesting appropriate values based on field types and constraints
6. Warning about common validation issues and how to avoid them

The generated prompt includes:
- Entity-specific field recommendations
- Required field identification and validation
- Data type formatting instructions (dates, numbers, lookups, option sets)
- Field naming convention guidance (logical names vs display names)
- Validation rule explanations and compliance tips
- Common field patterns and best practices

Perfect for guiding users through record creation with proper data structure and validation.")]
    public static ChatMessage CreateRecordPrompt(
        [Description("The logical name of the Dynamics 365 entity for which to create a record")]
        string entityName, 
        [Description("Natural language description of what the user wants to create or the business requirements")]
        string requirements)
    {
        var prompt = $@"You are helping to create a new {entityName} record in Dynamics 365.

Requirements: {requirements}

Please:
1. Identify the appropriate fields for the {entityName} entity
2. Map the requirements to the correct field names
3. Ensure required fields are included
4. Format the data as JSON for the create operation
5. Use proper data types (strings, integers, booleans, dates)

Example format:
{{
  ""field_name"": ""value"",
  ""another_field"": ""another_value""
}}

Provide the JSON object that can be used to create the record.";

        return new ChatMessage(ChatRole.User, prompt);
    }

    /// <summary>
    /// Generates a prompt for building OData filter expressions.
    /// </summary>
    [McpServerPrompt, Description(@"Generate expert guidance for building complex OData filter expressions for Dynamics 365 queries.

This prompt provides comprehensive assistance for creating OData filters including:
1. Converting natural language criteria into proper OData syntax
2. Explaining all available OData operators and their usage
3. Providing field-specific filtering guidance based on data types
4. Teaching proper date/time formatting for temporal queries
5. Demonstrating complex logical combinations with AND/OR/NOT
6. Showing relationship-based filtering for lookup fields
7. Performance optimization tips for filter expressions

The generated guidance covers:
- Complete OData operator reference with examples
- Field type-specific filtering patterns
- Date/time filtering with timezone considerations
- Lookup field filtering with GUID handling
- Option set filtering with numeric values
- String manipulation functions (contains, startswith, endswith)
- Mathematical operations for numeric fields
- Best practices for query performance

Perfect for users who need to create sophisticated queries with proper OData syntax.")]
    public static ChatMessage BuildFilterPrompt(
        [Description("The Dynamics 365 entity name that will be queried with the filter")]
        string entityName, 
        [Description("Natural language description of the filtering criteria or conditions needed")]
        string filterCriteria)
    {
        var prompt = $@"You are helping to build an OData filter expression for querying {entityName} records in Dynamics 365.

Filter criteria: {filterCriteria}

Please:
1. Convert the natural language criteria into proper OData filter syntax
2. Use appropriate operators (eq, ne, gt, lt, ge, le, contains, startswith, endswith)
3. Handle date/time fields with proper formatting
4. Use logical operators (and, or, not) as needed
5. Escape strings properly with single quotes

Common field types in Dynamics 365:
- Text fields: use 'contains' for partial matches, 'eq' for exact matches
- Lookup fields: use the ID value with 'eq'
- Date fields: use ISO format (2023-12-31T00:00:00Z)
- Boolean fields: use true/false
- Numbers: use numeric values without quotes

Example filters:
- Text: ""name eq 'Contoso'""
- Contains: ""contains(name, 'Corp')""
- Date: ""createdon gt 2023-01-01T00:00:00Z""
- Multiple: ""statuscode eq 1 and createdon gt 2023-01-01T00:00:00Z""

Provide the OData filter expression that can be used in the query.";

        return new ChatMessage(ChatRole.User, prompt);
    }

    /// <summary>
    /// Generates a prompt for data transformation and mapping.
    /// </summary>
    [McpServerPrompt, Description(@"Generate intelligent guidance for transforming and mapping data from various sources to Dynamics 365 entity structures.

This prompt provides comprehensive data transformation assistance including:
1. Analyzing source data structure and identifying field mappings
2. Converting data types to match Dynamics 365 field requirements
3. Handling missing or null values with appropriate defaults
4. Mapping external identifiers to Dynamics 365 lookup fields
5. Transforming date formats to ISO 8601 standard
6. Converting option set values to proper numeric codes
7. Validating data integrity and completeness before import

Data transformation capabilities:
- Multi-format data source analysis (JSON, XML, CSV, database)
- Automatic field mapping suggestions based on naming patterns
- Data type conversion with validation and error handling
- Lookup field resolution strategies
- Custom field mapping with publisher prefix handling
- Business rule validation integration
- Data cleansing and normalization recommendations

The guidance includes:
- Step-by-step transformation process
- Data quality validation checkpoints
- Common transformation pitfalls and solutions
- Performance considerations for large datasets
- Best practices for maintaining data integrity

Ideal for data migration, integration, and ETL scenarios.")]
    public static ChatMessage TransformDataPrompt(
        [Description("The source data to be transformed, in any format (JSON, CSV, XML, etc.)")]
        string sourceData, 
        [Description("The target Dynamics 365 entity name where the transformed data will be used")]
        string targetEntity)
    {
        var prompt = $@"You are helping to transform and map data for use with Dynamics 365 {targetEntity} entity.

Source data: {sourceData}

Please:
1. Analyze the source data structure
2. Map the fields to appropriate {targetEntity} entity fields
3. Transform data types as needed (strings, dates, numbers, lookups)
4. Handle missing or null values appropriately
5. Validate that required fields are present
6. Format the result as JSON suitable for Dynamics 365 operations

Common Dynamics 365 field naming conventions:
- Most fields use lowercase with underscores
- Lookup fields often end with 'id' (e.g., 'accountid', 'contactid')
- Custom fields have publisher prefix (e.g., 'new_customfield')
- Date fields expect ISO format
- Boolean fields use true/false
- Option set fields use integer values

Provide the transformed JSON object ready for Dynamics 365 operations.";

        return new ChatMessage(ChatRole.User, prompt);
    }

    /// <summary>
    /// Generates a prompt for troubleshooting Dynamics 365 operations.
    /// </summary>
    [McpServerPrompt, Description(@"Generate expert troubleshooting guidance for failed Dynamics 365 operations with comprehensive error analysis and resolution steps.

This prompt provides detailed troubleshooting assistance including:
1. Systematic error message analysis and root cause identification
2. Step-by-step resolution procedures for common and complex issues
3. Preventive measures to avoid similar issues in the future
4. Best practice recommendations for the specific operation type
5. Alternative approaches when standard solutions don't work
6. Performance optimization suggestions to prevent recurring problems
7. Integration with Dynamics 365 logging and diagnostic tools

Troubleshooting coverage includes:
- Authentication and authorization errors
- Field validation and data type issues
- Lookup field and relationship problems
- Permission and security role conflicts
- Business rule and workflow validation failures
- API rate limiting and throttling issues
- Concurrent update and locking problems
- Custom solution and plugin conflicts

The guidance provides:
- Detailed error analysis with probable causes
- Prioritized resolution steps from simple to complex
- Verification procedures to confirm fixes
- Monitoring recommendations to prevent recurrence
- Documentation links for advanced troubleshooting

Perfect for resolving operational issues and improving system reliability.")]
    public static ChatMessage TroubleshootPrompt(
        [Description("The type of operation that failed (create, read, update, delete, list, search)")]
        string operation, 
        [Description("The Dynamics 365 entity name involved in the failed operation")]
        string entityName, 
        [Description("The complete error message or description of the problem encountered")]
        string errorMessage)
    {
        var prompt = $@"You are helping to troubleshoot a Dynamics 365 operation that failed.

Operation: {operation}
Entity: {entityName}
Error message: {errorMessage}

Please:
1. Analyze the error message to identify the root cause
2. Provide specific steps to resolve the issue
3. Suggest preventive measures for future operations
4. Recommend best practices for this type of operation
5. If applicable, provide corrected field names or values

Common issues and solutions:
- Field not found: Check field logical name and entity schema
- Permission denied: Verify user has appropriate security roles
- Required field missing: Include all required fields in the operation
- Invalid data type: Ensure correct data types and formats
- Lookup field errors: Use proper GUID format and verify related record exists
- Date format errors: Use ISO format (YYYY-MM-DDTHH:mm:ssZ)

Provide a clear explanation of the issue and step-by-step resolution.";

        return new ChatMessage(ChatRole.User, prompt);
    }

    /// <summary>
    /// Generates a prompt for optimizing Dynamics 365 queries.
    /// </summary>
    [McpServerPrompt, Description(@"Generate advanced query optimization guidance for improving Dynamics 365 query performance and efficiency.

This prompt provides comprehensive query optimization assistance including:
1. Performance bottleneck identification and analysis
2. Index utilization strategies for faster data retrieval
3. Field selection optimization to reduce data transfer
4. Pagination strategies for large dataset handling
5. Filter optimization for improved execution plans
6. Relationship traversal optimization for complex queries
7. Caching strategies for frequently accessed data

Optimization techniques covered:
- Selective field retrieval with $select optimization
- Efficient filtering with indexed field utilization
- Proper pagination with $top and $skip parameters
- Related entity expansion optimization with $expand
- Query complexity reduction strategies
- Batch operation optimization for multiple requests
- Connection pooling and resource management

Performance analysis includes:
- Query execution time measurement and benchmarking
- Resource utilization assessment (CPU, memory, network)
- Concurrent user impact evaluation
- Scalability considerations for production environments
- Monitoring and alerting setup for performance tracking

The guidance provides:
- Before/after performance comparisons
- Specific optimization recommendations
- Implementation priority based on impact
- Testing strategies to validate improvements
- Monitoring setup for ongoing performance tracking

Ideal for improving application performance and user experience.")]
    public static ChatMessage OptimizeQueryPrompt(
        [Description("The Dynamics 365 entity being queried for optimization")]
        string entityName, 
        [Description("The current query or operation that needs performance optimization")]
        string currentQuery, 
        [Description("Description of the performance issue or metrics (slow response, timeout, high CPU, etc.)")]
        string performance)
    {
        var prompt = $@"You are helping to optimize a Dynamics 365 query for better performance.

Entity: {entityName}
Current query: {currentQuery}
Performance issue: {performance}

Please:
1. Analyze the current query for performance bottlenecks
2. Suggest optimizations for better performance
3. Recommend appropriate use of $select to limit returned fields
4. Advise on $top limits and pagination strategies
5. Suggest indexing strategies if applicable
6. Provide the optimized query

Query optimization best practices:
- Use $select to limit returned columns
- Use $top to limit result set size
- Avoid complex nested filters when possible
- Use indexed fields in filters when available
- Consider using $expand carefully for related records
- Use appropriate date ranges in filters
- Leverage server-side paging for large datasets

Provide the optimized query and explanation of improvements.";

        return new ChatMessage(ChatRole.User, prompt);
    }

    /// <summary>
    /// Generates a prompt for bulk operations planning.
    /// </summary>
    [McpServerPrompt, Description(@"Generate comprehensive planning guidance for executing bulk operations in Dynamics 365 safely and efficiently.

This prompt provides expert assistance for bulk operation planning including:
1. Optimal batch size determination based on operation type and system capacity
2. Performance impact assessment and mitigation strategies
3. Error handling and recovery procedures for failed operations
4. Monitoring and progress tracking implementation
5. Resource management and system load balancing
6. Data integrity validation throughout the process
7. Rollback and disaster recovery planning

Bulk operation planning covers:
- Capacity planning and resource allocation
- Timing strategies to minimize user impact
- Parallel processing vs sequential execution decisions
- Memory and connection management optimization
- Transaction boundary and commit strategy planning
- Failure detection and automatic retry mechanisms
- Progress reporting and user communication

Risk mitigation includes:
- Pre-operation validation and testing procedures
- Backup and recovery strategy implementation
- System performance monitoring during execution
- User access and concurrent operation management
- Data consistency verification and validation
- Emergency stop and rollback procedures

The guidance provides:
- Detailed execution timeline and milestones
- Resource requirement calculations
- Testing strategy for validation
- Monitoring and alerting setup
- Success criteria and completion verification
- Post-operation cleanup and optimization

Perfect for planning large-scale data operations while maintaining system stability and data integrity.")]
    public static ChatMessage BulkOperationPrompt(
        [Description("The type of bulk operation to perform (create, update, delete, import, export)")]
        string operation, 
        [Description("The Dynamics 365 entity name that will be processed in bulk")]
        string entityName, 
        [Description("The approximate number of records to be processed in the bulk operation")]
        int recordCount)
    {
        var prompt = $@"You are helping to plan a bulk {operation} operation for {recordCount} {entityName} records in Dynamics 365.

Please:
1. Recommend the best approach for handling this bulk operation
2. Suggest appropriate batch sizes and timing
3. Identify potential challenges and mitigation strategies
4. Recommend error handling and retry logic
5. Advise on monitoring and logging
6. Suggest testing strategies

Bulk operation considerations:
- API rate limits and throttling
- Transaction boundaries and rollback scenarios
- Data validation and error handling
- Performance impact on the system
- User experience during the operation
- Backup and recovery strategies

Provide a detailed plan for executing the bulk operation safely and efficiently.";

        return new ChatMessage(ChatRole.User, prompt);
    }
}

/// <summary>
/// Represents a chat message for MCP prompts.
/// </summary>
public class ChatMessage
{
    public ChatRole Role { get; }
    public string Content { get; }

    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Represents the role of a chat message.
/// </summary>
public enum ChatRole
{
    User,
    Assistant,
    System
}
# GitHub Copilot Agent Prompt: Build a Dynamic MCP Tool Registry for Microsoft Dynamics 365

You are building a dynamic MCP (Model Context Protocol) tool registry for integrating with Microsoft Dynamics 365 (Dataverse API). The goal is to eliminate the need for hardcoded tools by introspecting the Dynamics schema and auto-generating MCP tools at runtime.

---

## Requirements

### 🧠 Goal

Build a tool registry that:
- Connects to one or more Microsoft Dynamics 365 instances
- Introspects available entities, fields, and operations via the Dataverse Web API
- Dynamically generates MCP tools for each entity and operation
- Supports both standard and custom entities and fields
- Exposes a standard set of MCP entrypoints for tool discovery, registration, and execution

---

### 🔧 Tool Structure

Each generated tool must follow the MCP specification:
- Tool name: `dynamics_<operation>_<entity>[_by_<field>]`
- Description: concise explanation using entity metadata
- Input schema: generated from `EntityDefinitions` and `Attributes`
- Output: either raw JSON response or shaped structure
- MCP tools must support batched input and structured error reporting

---

### 🔒 Authentication

- Use OAuth 2.0 bearer tokens to authenticate to Dataverse Web API
- Tokens can be passed as part of `RegisterEndpoint` request
- No hardcoded credentials allowed

---

### 📤 Tool Execution APIs

Implement the following MCP-exposed tools:

1. `RegisterDynamicsEndpoint`
   - Registers a Dynamics instance by base URL, token, and optional prefix

2. `ListDynamicTools`
   - Returns all tools generated per endpoint (grouped by entity and operation)

3. `ExecuteDynamicTool`
   - Accepts a tool name and input JSON
   - Executes the corresponding Dataverse Web API call
   - Returns formatted response or error

4. `RefreshEndpointTools`
   - Re-introspects schema and regenerates tools for an endpoint

5. `UnregisterEndpoint`
   - Deletes all tools and metadata for the given endpoint

---

### 🔎 Metadata Sources

Use the following Dynamics 365 endpoints to generate tools:

- List entities: `GET /api/data/v9.2/EntityDefinitions`
- List attributes: `GET /api/data/v9.2/EntityDefinitions(LogicalName='<entity>')/Attributes`
- List actions: `GET /api/data/v9.2/EntityDefinitions(LogicalName='<entity>')/BoundActions`
- List functions: `GET /api/data/v9.2/EntityDefinitions(LogicalName='<entity>')/BoundFunctions`

---

### 🧱 Example Tool Generated

```json
{
  "tool_name": "dynamics_create_custom_entity",
  "description": "Create a new record in Custom Entity",
  "input_schema": {
    "type": "object",
    "properties": {
      "custom_name": { "type": "string" },
      "custom_score": { "type": "integer" }
    },
    "required": ["custom_name"]
  }
}


🧰 Technology Stack
	•	.NET 9
	•	System.Text.Json for serialization
	•	HttpClient for HTTP calls
	•	Dependency injection via Microsoft.Extensions.DependencyInjection
	•	XML documentation for all public methods
	•	ILogger for logging


Deliverables
	•	DynamicToolRegistry.cs with all logic and MCP entrypoints
	•	Internal caching of tools per endpoint
	•	Unit tests for tool registration, execution, and refresh
	•	Full XML documentation
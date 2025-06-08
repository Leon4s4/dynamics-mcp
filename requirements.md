# GitHub Copilot Agent Prompt: Build MCP Tools for Microsoft Dynamics 365 (C# .NET)

You are tasked with creating MCP (Model Context Protocol) tools for a Microsoft Dynamics 365 integration server using C# .NET. Build a comprehensive set of tools that enable AI assistants to interact with Dynamics 365 systems.

## Requirements

### Core MCP Tool Structure
Each tool should follow the MCP specification with:
- Tool name (snake_case with `dynamics_` prefix)
- Description explaining the tool's purpose
- Input schema using JSON Schema format
- Implementation that calls Dynamics 365 Web API using HttpClient

### Authentication & Security
- Use Microsoft.Graph SDK or custom OAuth 2.0 implementation
- Implement proper error handling and rate limiting
- Include input validation using Data Annotations
- Support both on-premises and cloud Dynamics 365
- Use env variables/user secrets to keep authentication credentials

### Technical Implementation Guidelines
- Use .NET 9
- Implement dependency injection with Microsoft.Extensions.DependencyInjection
- Use System.Text.Json for JSON serialization
- Implement proper async/await patterns
- Include comprehensive logging with ILogger
- Support configuration through appsettings.json and environment variables
- Use Official SDK for Dataverse and Dynamics 365 CE
- Document all tools with XML documentation


### Tool Categories to Implement
1. Customer Relationship Management

dynamics_get_contact - Retrieve contact information
dynamics_create_lead - Create new leads
dynamics_update_opportunity - Modify opportunities
dynamics_get_account_details - Fetch account information
dynamics_search_customers - Search across customer entities
2. Sales Pipeline Management

dynamics_get_sales_pipeline - Retrieve pipeline data with filters
dynamics_create_quote - Generate customer quotes
dynamics_update_deal_stage - Move deals through stages
dynamics_forecast_revenue - Get sales forecasting data
3. Activity Tracking

dynamics_log_activity - Record customer interactions
dynamics_schedule_appointment - Create calendar entries
dynamics_track_email - Log email communications
dynamics_create_task - Assign follow-up tasks
4. Analytics & Reporting

dynamics_generate_report - Create standard/custom reports
dynamics_get_kpi_metrics - Retrieve performance indicators
dynamics_export_data - Export data in various formats
dynamics_dashboard_insights - Get dashboard summaries
5. Customer Service

dynamics_create_case - Create support cases
dynamics_update_case_status - Modify case progression
dynamics_search_knowledge_base - Find relevant articles
dynamics_escalate_case - Handle case escalations
6. Marketing Operations

dynamics_create_campaign - Set up marketing campaigns
dynamics_track_campaign_metrics - Monitor performance
dynamics_segment_customers - Create customer segments
dynamics_manage_marketing_lists - Handle contact lists
7. Integration & Automation

dynamics_trigger_workflow - Execute Power Automate flows
dynamics_sync_external_data - Synchronize with other systems
dynamics_bulk_import - Handle bulk data operations
dynamics_webhook_subscribe - Set up event notifications
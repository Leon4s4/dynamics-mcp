#!/bin/bash

# Launch script for the Dynamics 365 MCP Server
# This script can be used by MCP clients to start the server

cd "$(dirname "$0")"
exec dotnet run --no-build

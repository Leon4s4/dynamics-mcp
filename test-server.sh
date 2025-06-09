#!/bin/bash

echo "Testing Dynamics 365 MCP Server..."

# Start the server in the background
dotnet run &
SERVER_PID=$!

# Give the server a moment to start
sleep 2

# Test that the server is running by checking if the process exists
if ps -p $SERVER_PID > /dev/null; then
    echo "✅ MCP Server started successfully (PID: $SERVER_PID)"
else
    echo "❌ Failed to start MCP Server"
    exit 1
fi

# Stop the server
kill $SERVER_PID
wait $SERVER_PID 2>/dev/null

echo "✅ MCP Server stopped successfully"
echo ""
echo "🎉 Dynamics 365 MCP Server is ready!"
echo ""
echo "Next steps:"
echo "1. Get an OAuth 2.0 bearer token for your Dynamics 365 instance"
echo "2. Start the server with: dotnet run"
echo "3. Use the RegisterDynamicsEndpoint tool to connect to your instance"
echo "4. Use ListDynamicTools to see all generated tools"
echo ""
echo "For more information, see README.md"

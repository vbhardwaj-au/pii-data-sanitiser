#!/bin/bash

# Quick Agent Deployment Script for Docker Desktop
# Usage: ./quick-deploy-agent.sh

echo "=== Quick Agent Deployment for Docker Desktop ==="
echo ""

# Step 1: Pre-register the agent
echo "Step 1: Pre-registering agent with portal..."
RESPONSE=$(curl -s -X POST http://localhost:7001/api/agent/pre-register \
  -H "Content-Type: application/json" \
  -H "X-API-Key: portal-admin-key-2024" \
  -d '{
    "name": "Docker-Agent-'$(date +%s)'",
    "hostName": "docker-desktop",
    "ipAddress": "172.17.0.2",
    "port": 5001,
    "version": "1.0.0",
    "environment": "Development",
    "location": "OnPremises",
    "cpuCores": 4,
    "memoryMB": 8192,
    "maxConcurrentJobs": 2,
    "priority": 1,
    "capabilities": {
      "canProcessSqlServer": true,
      "canProcessMySQL": false,
      "canProcessPostgreSQL": false,
      "canProcessOracle": false,
      "maxDatabaseSizeGB": 100,
      "maxRowsPerBatch": 10000,
      "supportedObfuscationTypes": ["Name", "Address", "Email", "Phone", "SSN", "CreditCard"]
    }
  }')

# Extract values from response
AGENT_ID=$(echo $RESPONSE | jq -r '.agentId')
AGENT_NAME=$(echo $RESPONSE | jq -r '.name')
API_KEY=$(echo $RESPONSE | jq -r '.apiKey')

if [ "$AGENT_ID" == "null" ]; then
    echo "Error: Failed to pre-register agent"
    echo "Response: $RESPONSE"
    exit 1
fi

echo "✓ Agent pre-registered successfully"
echo "  - Agent ID: $AGENT_ID"
echo "  - Agent Name: $AGENT_NAME"
echo ""

# Step 2: Build the Docker image
echo "Step 2: Building Docker image..."
cd "$(dirname "$0")"
docker build -t obfuscation-agent:latest -f Dockerfile.simple . > /dev/null 2>&1
echo "✓ Docker image built"
echo ""

# Step 3: Run the container
echo "Step 3: Starting Docker container..."
docker run -d \
  --name ${AGENT_NAME,,} \
  --restart unless-stopped \
  -e OBFUSCATION_Agent__AgentId=$AGENT_ID \
  -e OBFUSCATION_Agent__Name=$AGENT_NAME \
  -e OBFUSCATION_Agent__HostName=docker-desktop \
  -e OBFUSCATION_Portal__BaseUrl=http://host.docker.internal:7001 \
  -e OBFUSCATION_Portal__ApiKey=$API_KEY \
  obfuscation-agent:latest > /dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "✓ Container started successfully"
    echo ""
    echo "=== Deployment Complete ==="
    echo "Agent Name: $AGENT_NAME"
    echo "Agent ID: $AGENT_ID"
    echo "Container: ${AGENT_NAME,,}"
    echo ""
    echo "View agent status at: http://localhost:7002/Agents"
    echo "View container logs: docker logs ${AGENT_NAME,,}"
    echo "Stop container: docker stop ${AGENT_NAME,,}"
    echo "Remove container: docker rm ${AGENT_NAME,,}"
else
    echo "Error: Failed to start container"
    exit 1
fi
#!/bin/bash

echo "=========================================="
echo "Distributed Agent System Test Script"
echo "=========================================="
echo ""

API_URL="http://localhost:7001"
API_KEY=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $2"
    else
        echo -e "${RED}✗${NC} $2"
    fi
}

echo "1. Testing Agent Registration"
echo "------------------------------"

# Register first agent
echo "Registering Agent 1 (On-Premises PC 1)..."
RESPONSE1=$(curl -s -X POST $API_URL/api/agent/register \
  -H "Content-Type: application/json" \
  -d '{
    "name": "OnPrem-Agent-01",
    "hostName": "pc1.local",
    "ipAddress": "192.168.1.100",
    "port": 5001,
    "version": "1.0.0",
    "environment": "Production",
    "location": "OnPremises-DataCenter1",
    "cpuCores": 8,
    "memoryMB": 16384,
    "maxConcurrentJobs": 2,
    "priority": 1,
    "capabilities": {
      "canProcessSqlServer": true,
      "canProcessMySQL": false,
      "canProcessPostgreSQL": false,
      "canProcessOracle": false,
      "maxDatabaseSizeGB": 100,
      "maxRowsPerBatch": 10000,
      "supportedObfuscationTypes": ["Name", "Email", "Phone", "Address", "SSN"]
    }
  }')

AGENT1_ID=$(echo $RESPONSE1 | jq -r '.agentId')
AGENT1_KEY=$(echo $RESPONSE1 | jq -r '.apiKey')
print_status $? "Agent 1 registered with ID: $AGENT1_ID"

# Register second agent
echo "Registering Agent 2 (On-Premises PC 2)..."
RESPONSE2=$(curl -s -X POST $API_URL/api/agent/register \
  -H "Content-Type: application/json" \
  -d '{
    "name": "OnPrem-Agent-02",
    "hostName": "pc2.local",
    "ipAddress": "192.168.1.101",
    "port": 5002,
    "version": "1.0.0",
    "environment": "Production",
    "location": "OnPremises-DataCenter1",
    "cpuCores": 4,
    "memoryMB": 8192,
    "maxConcurrentJobs": 1,
    "priority": 2,
    "capabilities": {
      "canProcessSqlServer": true,
      "canProcessMySQL": true,
      "canProcessPostgreSQL": false,
      "canProcessOracle": false,
      "maxDatabaseSizeGB": 50,
      "maxRowsPerBatch": 5000,
      "supportedObfuscationTypes": ["Name", "Email", "Phone"]
    }
  }')

AGENT2_ID=$(echo $RESPONSE2 | jq -r '.agentId')
AGENT2_KEY=$(echo $RESPONSE2 | jq -r '.apiKey')
print_status $? "Agent 2 registered with ID: $AGENT2_ID"

# Register Azure agents
echo "Registering Azure Agents..."
for i in {1..3}; do
    RESPONSE=$(curl -s -X POST $API_URL/api/agent/register \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"Azure-Agent-0${i}\",
        \"hostName\": \"azure-vm-${i}.eastus.cloudapp.azure.com\",
        \"ipAddress\": \"10.0.1.${i}\",
        \"port\": $((5010 + i)),
        \"version\": \"1.0.0\",
        \"environment\": \"Production\",
        \"location\": \"Azure-EastUS\",
        \"cpuCores\": 16,
        \"memoryMB\": 32768,
        \"maxConcurrentJobs\": 4,
        \"priority\": 3,
        \"capabilities\": {
          \"canProcessSqlServer\": true,
          \"canProcessMySQL\": true,
          \"canProcessPostgreSQL\": true,
          \"canProcessOracle\": false,
          \"maxDatabaseSizeGB\": 500,
          \"maxRowsPerBatch\": 50000,
          \"supportedObfuscationTypes\": [\"Name\", \"Email\", \"Phone\", \"Address\", \"SSN\", \"CreditCard\", \"Date\"]
        }
      }")
    
    AZURE_ID=$(echo $RESPONSE | jq -r '.agentId')
    print_status $? "Azure Agent ${i} registered with ID: $AZURE_ID"
done

echo ""
echo "2. Testing Agent Discovery"
echo "------------------------------"

# Get all agents
echo "Fetching all registered agents..."
curl -s -X GET $API_URL/api/agent \
  -H "Authorization: Bearer $AGENT1_KEY" | jq '.[] | {name: .name, status: .status, location: .location}'

echo ""
echo "3. Testing Heartbeat"
echo "------------------------------"

# Send heartbeat for Agent 1
echo "Sending heartbeat from Agent 1..."
HEARTBEAT_RESPONSE=$(curl -s -X POST $API_URL/api/agent/$AGENT1_ID/heartbeat \
  -H "Authorization: Bearer $AGENT1_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "status": 1,
    "activeJobs": 0,
    "completedJobs": 0,
    "failedJobs": 0,
    "cpuUsage": 25.5,
    "memoryUsage": 45.2,
    "diskSpaceAvailable": 500000000000,
    "maxConcurrentJobs": 2
  }')

echo $HEARTBEAT_RESPONSE | jq '.'

echo ""
echo "4. Creating Test Jobs"
echo "------------------------------"

# Create a test product first
echo "Creating test product..."
PRODUCT_RESPONSE=$(curl -s -X POST $API_URL/api/products \
  -H "Authorization: Bearer $AGENT1_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "TestDB-Distributed",
    "description": "Test database for distributed processing",
    "connectionString": "Server=test;Database=TestDB;User Id=sa;Password=Pass123;",
    "databaseTechnology": "SqlServer",
    "isActive": true
  }')

PRODUCT_ID=$(echo $PRODUCT_RESPONSE | jq -r '.id')
echo "Product created with ID: $PRODUCT_ID"

# Create multiple jobs
echo "Creating jobs for distributed processing..."
for i in {1..5}; do
    JOB_RESPONSE=$(curl -s -X POST $API_URL/api/jobs \
      -H "Authorization: Bearer $AGENT1_KEY" \
      -H "Content-Type: application/json" \
      -d "{
        \"productId\": \"$PRODUCT_ID\",
        \"jobName\": \"Obfuscation-Job-${i}\",
        \"description\": \"Test job ${i} for distributed processing\",
        \"priority\": $((RANDOM % 3 + 1)),
        \"configuration\": {
          \"tables\": [\"Table${i}\"],
          \"batchSize\": 1000
        }
      }")
    
    JOB_ID=$(echo $JOB_RESPONSE | jq -r '.id')
    echo "Created Job ${i} with ID: $JOB_ID"
done

echo ""
echo "5. Testing Job Distribution"
echo "------------------------------"

# Test workload distribution
echo "Testing workload distribution strategies..."

# RoundRobin
echo -e "\n${YELLOW}Round Robin Distribution:${NC}"
curl -s -X POST $API_URL/api/jobs/distribute \
  -H "Authorization: Bearer $AGENT1_KEY" \
  -H "Content-Type: application/json" \
  -d '{"strategy": "RoundRobin"}' | jq '.assignments[] | {jobId: .jobId, agentName: .agentName}'

# LoadBalanced
echo -e "\n${YELLOW}Load Balanced Distribution:${NC}"
curl -s -X POST $API_URL/api/jobs/distribute \
  -H "Authorization: Bearer $AGENT1_KEY" \
  -H "Content-Type: application/json" \
  -d '{"strategy": "LoadBalanced"}' | jq '.assignments[] | {jobId: .jobId, agentName: .agentName}'

echo ""
echo "6. Testing Job Request from Agent"
echo "------------------------------"

# Agent requests a job
echo "Agent 1 requesting a job..."
JOB_REQUEST=$(curl -s -X POST $API_URL/api/agent/jobs/request \
  -H "Authorization: Bearer $AGENT1_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "maxConcurrentJobs": 2
  }')

echo $JOB_REQUEST | jq '.'

echo ""
echo "7. Testing Progress Reporting"
echo "------------------------------"

# Get a job ID for testing
JOB_ID=$(echo $JOB_REQUEST | jq -r '.job.jobId' 2>/dev/null || echo "")

if [ ! -z "$JOB_ID" ] && [ "$JOB_ID" != "null" ]; then
    echo "Updating job progress..."
    
    # Update job status to Running
    curl -s -X POST $API_URL/api/agent/jobs/$JOB_ID/status \
      -H "Authorization: Bearer $AGENT1_KEY" \
      -H "Content-Type: application/json" \
      -d '{
        "status": "Running",
        "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'",
        "message": "Job started"
      }'
    
    # Update progress
    curl -s -X POST $API_URL/api/agent/jobs/$JOB_ID/progress \
      -H "Authorization: Bearer $AGENT1_KEY" \
      -H "Content-Type: application/json" \
      -d '{
        "progressPercentage": 45.5,
        "rowsProcessed": 12345,
        "rowsAffected": 10234,
        "currentTable": "Customers",
        "currentOperation": "Obfuscating names",
        "rowsPerSecond": 1523.4
      }'
    
    echo "Progress updated for job $JOB_ID"
fi

echo ""
echo "8. Testing Agent Health Monitoring"
echo "------------------------------"

# Get agent health status
echo "Checking agent health status..."
curl -s -X GET $API_URL/api/agent/health \
  -H "Authorization: Bearer $AGENT1_KEY" | jq '.'

echo ""
echo "9. Testing Result Aggregation"
echo "------------------------------"

# Get aggregated results
echo "Fetching aggregated results..."
curl -s -X GET $API_URL/api/jobs/results/aggregate \
  -H "Authorization: Bearer $AGENT1_KEY" | jq '.'

echo ""
echo "=========================================="
echo "Test Complete!"
echo "=========================================="
echo ""
echo "Summary:"
echo "- Registered 5 agents (2 on-premises, 3 Azure)"
echo "- Created test jobs for distributed processing"
echo "- Tested job distribution strategies"
echo "- Tested agent-to-portal communication"
echo "- Tested progress reporting and monitoring"
echo ""
echo "To monitor the system:"
echo "1. Web UI: http://localhost:7002"
echo "2. API Swagger: http://localhost:7001/swagger"
echo "3. Agent logs: Check the running agent console"
# Distributed Agent System - Testing Guide

## Prerequisites
- Portal API running on http://localhost:7001
- Portal Web UI running on http://localhost:7002
- SQL Server (local or Azure) with portal database

## Step-by-Step Testing

### 1. Start the Portal Services

```bash
# Terminal 1 - Start API
cd portal/api
dotnet run

# Terminal 2 - Start Web UI
cd portal/web
dotnet run
```

### 2. Register Multiple Agents

You can simulate multiple agents by running the agent application with different configurations:

#### Agent 1 - On-Premises PC 1
```bash
# Terminal 3
cd portal/agent-client/ObfuscationAgent

# Edit appsettings.json:
# - Name: "OnPrem-Agent-01"
# - HostName: "pc1-onprem"
# - Port: 5001
# - Location: "OnPremises-DC1"

dotnet run
```

#### Agent 2 - On-Premises PC 2
```bash
# Terminal 4
cd portal/agent-client/ObfuscationAgent

# Copy to new folder and edit appsettings.json:
cp -r . ../ObfuscationAgent2
cd ../ObfuscationAgent2

# Edit appsettings.json:
# - Name: "OnPrem-Agent-02"
# - HostName: "pc2-onprem"
# - Port: 5002
# - Location: "OnPremises-DC1"
# - MaxConcurrentJobs: 1

dotnet run
```

#### Agent 3 - Azure VM
```bash
# Terminal 5
cd portal/agent-client/ObfuscationAgent

# Copy to new folder and edit appsettings.json:
cp -r . ../ObfuscationAgentAzure
cd ../ObfuscationAgentAzure

# Edit appsettings.json:
# - Name: "Azure-Agent-01"
# - HostName: "azure-vm-01"
# - Port: 5010
# - Location: "Azure-EastUS"
# - CpuCores: 16
# - MemoryMB: 32768
# - MaxConcurrentJobs: 4

dotnet run
```

### 3. Test Agent Registration via API

```bash
# Register a test agent directly
curl -X POST http://localhost:7001/api/agent/register \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test-Agent",
    "hostName": "test-host",
    "port": 6000,
    "version": "1.0.0",
    "environment": "Test",
    "location": "TestLocation",
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
      "supportedObfuscationTypes": ["Name", "Email", "Phone"]
    }
  }'
```

### 4. Monitor via Web UI

Open http://localhost:7002 in your browser to see:
- Registered agents and their status
- Active jobs and their progress
- System health metrics
- Job distribution across agents

### 5. Create and Distribute Jobs

Via the Web UI or API:

```bash
# Create a product (database configuration)
curl -X POST http://localhost:7001/api/products \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "name": "TestDatabase",
    "connectionString": "Server=localhost;Database=TestDB;Integrated Security=true;",
    "databaseTechnology": "SqlServer"
  }'

# Create obfuscation jobs
curl -X POST http://localhost:7001/api/jobs \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "productId": "product-guid-here",
    "jobName": "Obfuscate Customer Data",
    "priority": 1,
    "configuration": {
      "tables": ["Customers", "Orders"],
      "batchSize": 1000
    }
  }'
```

### 6. Test Workload Distribution

The system supports multiple distribution strategies:

- **Round Robin**: Jobs distributed evenly across agents
- **Load Balanced**: Jobs assigned to least loaded agents
- **Priority Based**: High-priority jobs to high-priority agents
- **Optimal Performance**: Based on agent capabilities and performance history
- **Geographic**: Jobs assigned to agents in same location

### 7. Simulate Job Processing

Watch the agent console output to see:
- Agent registration confirmation
- Heartbeat messages (every 30 seconds)
- Job polling (every 10 seconds)
- Job assignment and execution
- Progress reporting
- Result upload

### 8. Test Failure Scenarios

#### Agent Failure
1. Stop an agent while it's processing a job
2. Watch the portal reassign the job to another agent

#### Network Failure
1. Disconnect an agent from the network
2. Portal marks agent as offline after missing heartbeats
3. Jobs redistributed to healthy agents

#### Job Failure
1. Create a job with invalid configuration
2. Agent reports failure
3. Portal retries with another agent (up to MaxRetries)

## Expected Results

✅ **Successful Registration**: Each agent receives unique ID and API key
✅ **Heartbeat**: Agents report status every 30 seconds
✅ **Job Distribution**: Jobs assigned based on selected strategy
✅ **Progress Tracking**: Real-time progress updates in Web UI
✅ **Fault Tolerance**: Failed jobs reassigned to healthy agents
✅ **Scalability**: System handles multiple concurrent agents

## Monitoring Commands

```bash
# Check agent status via API
curl http://localhost:7001/api/agent -H "X-API-Key: your-key"

# Check job queue
curl http://localhost:7001/api/jobs/queue -H "X-API-Key: your-key"

# Get system health
curl http://localhost:7001/api/health

# View agent logs
# Check the console output of each running agent
```

## Troubleshooting

### Agent Won't Register
- Check API is running on port 7001
- Verify unique hostname:port combination
- Check firewall settings

### Jobs Not Being Assigned
- Verify agents have available capacity
- Check job priority settings
- Ensure agents support required capabilities

### Heartbeat Failures
- Check network connectivity
- Verify API key is valid
- Check agent state file for credentials

## Production Deployment

For real deployment across multiple machines:

1. **On-Premises Agents**:
   - Install .NET 8 runtime
   - Copy agent application
   - Configure appsettings.json with unique values
   - Run as Windows Service or systemd service

2. **Azure VM Agents**:
   - Deploy via Azure DevOps or ARM templates
   - Use Azure Key Vault for API keys
   - Configure auto-scaling based on job queue

3. **Portal Deployment**:
   - Deploy API to Azure App Service
   - Use Azure SQL Database
   - Configure Application Insights for monitoring
   - Use Azure Front Door for load balancing

## Performance Benchmarks

Expected performance with distributed agents:

| Configuration | Rows/Second | Notes |
|--------------|-------------|-------|
| 1 Agent | ~1,500 | Single machine baseline |
| 2 Agents | ~2,800 | Near-linear scaling |
| 5 Agents | ~6,500 | Good parallelization |
| 10 Agents | ~12,000 | Optimal configuration |

## Next Steps

1. Configure Azure Blob Storage for mapping files
2. Implement agent auto-scaling
3. Add Prometheus metrics export
4. Configure alerting for agent failures
5. Implement job scheduling and priorities
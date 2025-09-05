# Deploying Distributed Obfuscation Agents

This guide explains how to deploy obfuscation agents on different machines to process SQL databases in a distributed manner.

## Architecture Overview

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│   Portal Web    │────────▶│   Portal API    │◀────────│     Agent 1     │
│  (localhost:    │         │  (localhost:    │         │  (Machine A)    │
│     7002)       │         │     7001)       │         └─────────────────┘
└─────────────────┘         └────────┬────────┘                 │
                                      │                          │
                                      │                          ▼
                                      │                 ┌─────────────────┐
                                      │                 │   SQL Server    │
                                      │                 │   Database 1    │
                                      │                 └─────────────────┘
                                      │
                                      │         ┌─────────────────┐
                                      └─────────│     Agent 2     │
                                                │  (Machine B)    │
                                                └─────────────────┘
                                                         │
                                                         ▼
                                                ┌─────────────────┐
                                                │   SQL Server    │
                                                │   Database 2    │
                                                └─────────────────┘
```

## Prerequisites

1. **Portal API Running**: Ensure the Portal API is running on your main machine (localhost:7001)
2. **.NET 8 SDK**: Install on each agent machine
3. **Network Access**: Agents need to reach the Portal API (port 7001)
4. **SQL Server Access**: Each agent needs access to its target SQL database

## Step-by-Step Deployment Guide

### Step 1: Prepare the Agent Application

1. **Copy the agent project** to each target machine:
   ```bash
   # On your main machine, create a deployment package
   cd /Users/vikas/Code/published/treat-sql-db/portal/agent-client
   tar -czf obfuscation-agent.tar.gz ObfuscationAgent/
   ```

2. **Transfer to target machines** using SCP or any file transfer method:
   ```bash
   # Example for Machine A
   scp obfuscation-agent.tar.gz user@machine-a:/path/to/deployment/

   # Example for Machine B
   scp obfuscation-agent.tar.gz user@machine-b:/path/to/deployment/
   ```

### Step 2: Configure Agent on Machine A

1. **SSH into Machine A**:
   ```bash
   ssh user@machine-a
   ```

2. **Extract the agent**:
   ```bash
   cd /path/to/deployment
   tar -xzf obfuscation-agent.tar.gz
   cd ObfuscationAgent
   ```

3. **Configure appsettings.json**:
   ```bash
   nano appsettings.json
   ```

   Update the configuration:
   ```json
   {
     "AgentConfiguration": {
       "AgentId": "agent-machine-a-001",
       "AgentName": "SQL Agent Machine A",
       "HostName": "machine-a.local",
       "Port": 8080,
       "Environment": "Production",
       "Location": "DataCenter-A",
       "MaxConcurrentJobs": 2,
       "Priority": 1,
       "Capabilities": {
         "CanProcessSqlServer": true,
         "CanProcessMySQL": false,
         "CanProcessPostgreSQL": false,
         "CanProcessOracle": false,
         "MaxDatabaseSizeGB": 500,
         "MaxRowsPerBatch": 10000
       }
     },
     "PortalConfiguration": {
       "ApiUrl": "http://YOUR_MAIN_MACHINE_IP:7001",
       "ApiKey": "portal-admin-key-2024",
       "HeartbeatIntervalSeconds": 30,
       "JobPollingIntervalSeconds": 10
     },
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft": "Warning",
         "Microsoft.Hosting.Lifetime": "Information"
       }
     }
   }
   ```

   **IMPORTANT**: Replace `YOUR_MAIN_MACHINE_IP` with the actual IP address of your main machine (e.g., 192.168.1.100)

4. **Create a service file** (for Linux):
   ```bash
   sudo nano /etc/systemd/system/obfuscation-agent.service
   ```

   Add:
   ```ini
   [Unit]
   Description=Obfuscation Agent Service
   After=network.target

   [Service]
   Type=notify
   User=www-data
   WorkingDirectory=/path/to/deployment/ObfuscationAgent
   ExecStart=/usr/bin/dotnet run
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=obfuscation-agent
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

5. **Start the agent**:
   ```bash
   # For testing
   dotnet run

   # Or as a service
   sudo systemctl daemon-reload
   sudo systemctl enable obfuscation-agent
   sudo systemctl start obfuscation-agent
   sudo systemctl status obfuscation-agent
   ```

### Step 3: Configure Agent on Machine B

Repeat Step 2 for Machine B with these changes in appsettings.json:

```json
{
  "AgentConfiguration": {
    "AgentId": "agent-machine-b-001",
    "AgentName": "SQL Agent Machine B",
    "HostName": "machine-b.local",
    "Port": 8080,
    "Environment": "Production",
    "Location": "DataCenter-B",
    // ... rest of configuration
  }
  // ... rest same as Machine A
}
```

### Step 4: Configure Portal API to Accept Remote Agents

1. **Update API appsettings.json** on your main machine:
   ```bash
   cd /Users/vikas/Code/published/treat-sql-db/portal/api
   nano appsettings.json
   ```

2. **Ensure API is listening on all interfaces**:
   ```json
   {
     "Urls": "http://0.0.0.0:7001",
     // ... rest of configuration
   }
   ```

3. **Configure firewall** (if needed):
   ```bash
   # macOS
   sudo pfctl -e
   sudo pfctl -f /etc/pf.conf

   # Linux
   sudo ufw allow 7001/tcp

   # Windows
   netsh advfirewall firewall add rule name="Portal API" dir=in action=allow protocol=TCP localport=7001
   ```

### Step 5: Create Agent Registration in Database

The agents will auto-register when they connect. However, you can pre-register them:

```sql
-- Connect to your Portal database
INSERT INTO ObfuscationAgents (
    Id,
    Name,
    HostName,
    IpAddress,
    Port,
    Version,
    Environment,
    Location,
    Status,
    MaxConcurrentJobs,
    IsEnabled,
    Priority,
    RegisteredAt
) VALUES 
(
    NEWID(),
    'SQL Agent Machine A',
    'machine-a.local',
    '192.168.1.101',
    8080,
    '1.0.0',
    'Production',
    'DataCenter-A',
    0, -- Offline initially
    2,
    1,
    1,
    GETUTCDATE()
),
(
    NEWID(),
    'SQL Agent Machine B',
    'machine-b.local',
    '192.168.1.102',
    8080,
    '1.0.0',
    'Production',
    'DataCenter-B',
    0, -- Offline initially
    2,
    1,
    1,
    GETUTCDATE()
);
```

## Agent Features

Each agent can:
1. **Process multiple databases** concurrently
2. **Auto-register** with the portal
3. **Send heartbeats** to indicate health
4. **Report metrics** (CPU, Memory, Job status)
5. **Handle job assignments** from the portal
6. **Process specific database types** (SQL Server, MySQL, etc.)

## Monitoring Agents

1. **Via Web Portal**: 
   - Navigate to http://localhost:7002/Agents
   - View agent status, metrics, and job history

2. **Via API**:
   ```bash
   curl http://localhost:7001/api/agents \
     -H "X-API-Key: portal-admin-key-2024" | jq
   ```

3. **Agent Logs**:
   ```bash
   # On each agent machine
   tail -f /path/to/deployment/ObfuscationAgent/logs/*.log

   # Or if using systemd
   sudo journalctl -u obfuscation-agent -f
   ```

## Troubleshooting

### Agent Not Appearing in Portal
1. Check network connectivity:
   ```bash
   curl http://YOUR_MAIN_MACHINE_IP:7001/api/health
   ```

2. Check agent logs for connection errors

3. Verify firewall rules

### Agent Shows as Offline
1. Check heartbeat configuration
2. Verify agent is running:
   ```bash
   ps aux | grep dotnet
   ```

### Jobs Not Processing
1. Check agent capabilities match job requirements
2. Verify database connectivity from agent
3. Check agent MaxConcurrentJobs setting

## Security Considerations

1. **Use HTTPS** in production:
   - Configure SSL certificates for API
   - Update agent configurations to use https://

2. **Secure API Key**:
   - Use environment variables instead of appsettings.json
   - Rotate keys regularly

3. **Network Security**:
   - Use VPN or private network between agents and portal
   - Implement IP whitelisting in API

4. **Database Security**:
   - Use separate credentials for each agent
   - Limit permissions to required databases only

## Example Windows Service Setup

For Windows machines, create a Windows Service:

```powershell
# Install as Windows Service
sc.exe create "ObfuscationAgent" binPath="C:\Program Files\dotnet\dotnet.exe C:\Agents\ObfuscationAgent\ObfuscationAgent.dll" start=auto

# Start the service
sc.exe start ObfuscationAgent

# Check status
sc.exe query ObfuscationAgent
```

## Scaling Considerations

1. **Load Balancing**: Deploy multiple agents for large databases
2. **Geographic Distribution**: Place agents close to databases
3. **Resource Allocation**: Adjust MaxConcurrentJobs based on machine specs
4. **Priority Settings**: Higher priority agents get jobs first

## Maintenance

1. **Update Agents**:
   ```bash
   # Stop agent
   sudo systemctl stop obfuscation-agent
   
   # Update files
   # ... copy new files ...
   
   # Start agent
   sudo systemctl start obfuscation-agent
   ```

2. **Monitor Performance**:
   - Check CPU/Memory usage regularly
   - Review job completion times
   - Analyze error logs

3. **Database Cleanup**:
   - Remove old job records periodically
   - Archive completed job logs

## Support

For issues or questions:
1. Check agent logs first
2. Verify network connectivity
3. Review this guide
4. Check Portal API logs
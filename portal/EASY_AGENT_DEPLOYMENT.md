# Easy Agent Deployment Guide

Deploy multiple obfuscation agents with just a few commands! This guide provides the **easiest possible way** to deploy agents both on-premises and in Azure.

## 🚀 Quick Start (Fastest Deployment)

### One-Command Deployment

**Linux/Mac:**
```bash
cd portal/agent-client
chmod +x deploy-agents.sh
./deploy-agents.sh deploy-all
```

**Windows:**
```powershell
cd portal\agent-client
.\deploy-agents.ps1 -Action QuickDeploy
```

This will:
1. Build the Docker image
2. Deploy 2 on-premise agents (SQL Server + PostgreSQL)
3. Deploy 7 Azure agents (if you choose)
4. Show status of all deployed agents

## 📋 Prerequisites

1. **Docker Desktop** installed
2. **Portal API** running on your machine
3. **Azure CLI** (only for Azure deployment)
4. **Network access** between agents and Portal API

## 🏗️ Architecture

```
Your Machine (Portal)          On-Premise              Azure Cloud
┌──────────────────┐      ┌──────────────┐      ┌──────────────┐
│   Portal API     │◀─────│  SQL Agent   │      │  7x Azure    │
│  (port 7001)     │      │              │      │   Agents     │
│                  │◀─────│ PostgreSQL   │      │              │
│   Portal Web     │      │   Agent      │      │  Container   │
│  (port 7002)     │      └──────────────┘      │  Instances   │
└──────────────────┘                            └──────────────┘
```

## 🎯 Deployment Options

### Option 1: Interactive Menu (Easiest)

Just run the script without parameters:

```bash
# Linux/Mac
./deploy-agents.sh

# Windows
.\deploy-agents.ps1
```

You'll see a menu:
```
======================================
   Obfuscation Agent Deployment Tool
======================================
1. Quick Deploy (Recommended)  ← Choose this!
2. Build Docker Image
3. Deploy On-Premise Agents
4. Deploy Azure Agents
...
```

### Option 2: Docker Compose (On-Premise Only)

```bash
# Set your Portal API URL
export PORTAL_API_URL=http://YOUR_IP:7001
export PORTAL_API_KEY=portal-admin-key-2024

# Deploy both on-premise agents
docker-compose up -d
```

### Option 3: Kubernetes (Maximum Scale)

```bash
# Deploy to Kubernetes cluster
kubectl apply -f kubernetes/

# Scale to any number of agents
kubectl scale deployment obfuscation-agent --replicas=20
```

## 🔧 Configuration

### Environment Variables

Create a `.env` file:
```env
PORTAL_API_URL=http://192.168.1.100:7001
PORTAL_API_KEY=portal-admin-key-2024
DOCKER_REGISTRY=youracr.azurecr.io
```

### Agent Capabilities

Each agent can be configured for specific databases:

| Agent Type | Databases | Location | Count |
|------------|-----------|----------|-------|
| On-Prem SQL | MS SQL Server | Local | 1 |
| On-Prem PG | PostgreSQL | Local | 1 |
| Azure Agents | Azure SQL | Cloud | 7 |

## 🌟 Super Easy Azure Deployment

### Step 1: Prepare
```bash
# Login to Azure
az login

# Login to your container registry
az acr login --name youracr
```

### Step 2: Deploy
```bash
# Deploy 7 agents to Azure with one command
./deploy-agents.sh deploy-azure 7
```

Or use the Azure Portal:
1. Click "Deploy to Azure" button
2. Fill in parameters
3. Click "Review + Create"
4. Done!

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fyourrepo%2Fagents%2Fazure-deploy%2Fazure-container-instances.json)

## 📊 Monitoring

### View All Agents
```bash
# Show status of all agents
./deploy-agents.sh status
```

### Web Portal
Navigate to: http://localhost:7002/Agents

### Check Logs
```bash
# View specific agent logs
docker logs obfuscation-agent-sql -f
```

## 🔄 Scaling

### Scale On-Premise
```yaml
# Edit docker-compose.yml
services:
  agent-sql:
    scale: 3  # Run 3 SQL agents
```

### Scale Azure
```bash
# Update agent count
az container create --resource-group obfuscation-agents-rg \
  --file azure-deploy/azure-container-instances.json \
  --parameters agentCount=15
```

### Scale Kubernetes
```bash
kubectl scale deployment obfuscation-agent --replicas=50
```

## 🛠️ Troubleshooting

### Agent Not Connecting?
```bash
# Check connectivity
curl http://YOUR_PORTAL_IP:7001/api/health

# Check firewall
sudo ufw allow 7001/tcp  # Linux
```

### Azure Deployment Failed?
```bash
# Check resource group
az group show --name obfuscation-agents-rg

# Check container instances
az container list --resource-group obfuscation-agents-rg
```

## 💡 Tips

1. **Use Docker Compose** for on-premise agents
2. **Use Azure Container Instances** for cloud agents
3. **Use Kubernetes** for large-scale deployments
4. **Set resource limits** to prevent overconsumption
5. **Monitor logs** regularly

## 📝 Example Scenarios

### Scenario 1: Small Setup (2 Agents)
```bash
docker-compose up -d
```

### Scenario 2: Medium Setup (9 Agents)
```bash
# On-premise: 2 agents
docker-compose up -d

# Azure: 7 agents
./deploy-agents.sh deploy-azure 7
```

### Scenario 3: Large Setup (50+ Agents)
```bash
# Use Kubernetes
kubectl apply -f kubernetes/
kubectl scale deployment obfuscation-agent --replicas=50
```

## 🔐 Security

1. **Use secrets** for API keys
2. **Enable HTTPS** for Portal API
3. **Use managed identities** in Azure
4. **Restrict network access** with firewalls

## 📞 Support

- Check agent logs: `docker logs <agent-name>`
- Portal logs: Check http://localhost:7002/Agents
- API health: `curl http://localhost:7001/api/health`

## 🎉 That's It!

You now have multiple agents processing databases in parallel. The Portal will automatically distribute work among all available agents.

Monitor everything at: **http://localhost:7002/Agents**
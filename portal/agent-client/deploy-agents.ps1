# Obfuscation Agent Deployment Script for Windows
# Supports both on-premise and Azure deployments

param(
    [Parameter(Position=0)]
    [string]$Action = "Menu",
    
    [Parameter()]
    [string]$PortalApiUrl = $env:PORTAL_API_URL,
    
    [Parameter()]
    [string]$PortalApiKey = $env:PORTAL_API_KEY,
    
    [Parameter()]
    [string]$DockerRegistry = "youracr.azurecr.io",
    
    [Parameter()]
    [int]$AgentCount = 7,
    
    [Parameter()]
    [string]$ResourceGroup = "obfuscation-agents-rg",
    
    [Parameter()]
    [string]$Location = "eastus"
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# Check prerequisites
function Test-Prerequisites {
    param([bool]$CheckAzure = $false)
    
    Write-ColorOutput "Checking prerequisites..." "Yellow"
    
    # Check Docker
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-ColorOutput "Docker is not installed. Please install Docker Desktop first." "Red"
        exit 1
    }
    
    # Check Azure CLI for Azure deployments
    if ($CheckAzure) {
        if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
            Write-ColorOutput "Azure CLI is not installed. Please install Azure CLI first." "Red"
            exit 1
        }
    }
    
    Write-ColorOutput "Prerequisites check passed!" "Green"
}

# Build Docker image
function Build-AgentImage {
    Write-ColorOutput "Building Docker image..." "Yellow"
    
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    Set-Location $scriptPath
    
    docker build -t obfuscation-agent:latest .
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "Docker image built successfully!" "Green"
    } else {
        Write-ColorOutput "Failed to build Docker image" "Red"
        exit 1
    }
}

# Push image to registry
function Push-AgentImage {
    Write-ColorOutput "Pushing image to registry..." "Yellow"
    
    # Tag for registry
    docker tag obfuscation-agent:latest "$DockerRegistry/obfuscation-agent:latest"
    
    # Push to registry
    docker push "$DockerRegistry/obfuscation-agent:latest"
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "Image pushed to registry successfully!" "Green"
    } else {
        Write-ColorOutput "Failed to push image to registry" "Red"
        exit 1
    }
}

# Deploy on-premise agents
function Deploy-OnPremiseAgents {
    Write-ColorOutput "Deploying on-premise agents..." "Yellow"
    
    # Check if Portal API URL is set
    if (-not $PortalApiUrl) {
        $PortalApiUrl = Read-Host "Enter Portal API URL (e.g., http://192.168.1.100:7001)"
    }
    
    if (-not $PortalApiKey) {
        $secureKey = Read-Host "Enter Portal API Key" -AsSecureString
        $PortalApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        )
    }
    
    # Set environment variables for docker-compose
    $env:PORTAL_API_URL = $PortalApiUrl
    $env:PORTAL_API_KEY = $PortalApiKey
    
    # Deploy using docker-compose
    docker-compose up -d
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "On-premise agents deployed successfully!" "Green"
        docker-compose ps
    } else {
        Write-ColorOutput "Failed to deploy on-premise agents" "Red"
        exit 1
    }
}

# Deploy Azure agents
function Deploy-AzureAgents {
    Write-ColorOutput "Deploying $AgentCount agents to Azure..." "Yellow"
    
    # Login to Azure if not already logged in
    $account = az account show 2>$null
    if (-not $account) {
        Write-ColorOutput "Please login to Azure..." "Yellow"
        az login
    }
    
    # Create resource group if it doesn't exist
    $rgExists = az group show --name $ResourceGroup 2>$null
    if (-not $rgExists) {
        Write-ColorOutput "Creating resource group: $ResourceGroup" "Yellow"
        az group create --name $ResourceGroup --location $Location
    }
    
    # Create parameters file
    $parameters = @{
        agentCount = @{ value = $AgentCount }
        containerImage = @{ value = "$DockerRegistry/obfuscation-agent:latest" }
        portalApiUrl = @{ value = $PortalApiUrl }
        portalApiKey = @{ value = $PortalApiKey }
        location = @{ value = $Location }
    }
    
    $parametersJson = $parameters | ConvertTo-Json -Depth 3
    $parametersJson | Out-File -FilePath ".\azure-deploy\parameters.json"
    
    # Deploy using ARM template
    Write-ColorOutput "Deploying Azure Container Instances..." "Yellow"
    
    az deployment group create `
        --resource-group $ResourceGroup `
        --template-file azure-deploy/azure-container-instances.json `
        --parameters azure-deploy/parameters.json
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "Azure agents deployed successfully!" "Green"
        
        # List deployed container instances
        az container list --resource-group $ResourceGroup --output table
    } else {
        Write-ColorOutput "Failed to deploy Azure agents" "Red"
        exit 1
    }
}

# Deploy single agent locally for testing
function Deploy-SingleAgent {
    param(
        [string]$AgentName = "test-agent",
        [string]$DatabaseType = "SqlServer"
    )
    
    Write-ColorOutput "Deploying single agent: $AgentName" "Yellow"
    
    $envVars = @(
        "-e", "AgentConfiguration__AgentId=$AgentName",
        "-e", "AgentConfiguration__AgentName=$AgentName",
        "-e", "AgentConfiguration__Environment=Development",
        "-e", "AgentConfiguration__Location=Local",
        "-e", "AgentConfiguration__MaxConcurrentJobs=1",
        "-e", "AgentConfiguration__Capabilities__CanProcessSqlServer=$($DatabaseType -eq 'SqlServer')",
        "-e", "AgentConfiguration__Capabilities__CanProcessPostgreSQL=$($DatabaseType -eq 'PostgreSQL')",
        "-e", "PortalConfiguration__ApiUrl=$PortalApiUrl",
        "-e", "PortalConfiguration__ApiKey=$PortalApiKey"
    )
    
    docker run -d `
        --name $AgentName `
        $envVars `
        -p 8080:8080 `
        obfuscation-agent:latest
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "Agent $AgentName deployed successfully!" "Green"
    } else {
        Write-ColorOutput "Failed to deploy agent" "Red"
    }
}

# Show agent status
function Show-AgentStatus {
    Write-ColorOutput "Checking agent status..." "Yellow"
    
    # Check local Docker containers
    Write-ColorOutput "`nLocal Docker Agents:" "Green"
    docker ps --filter "name=obfuscation-agent" --format "table {{.Names}}`t{{.Status}}`t{{.Ports}}"
    
    # Check Azure agents if logged in
    $account = az account show 2>$null
    if ($account) {
        Write-ColorOutput "`nAzure Container Instances:" "Green"
        az container list --query "[?contains(name, 'obfuscation-agent')].{Name:name, Status:instanceView.state, Location:location}" --output table
    }
    
    # Check Portal API for registered agents
    if ($PortalApiUrl) {
        Write-ColorOutput "`nRegistered Agents in Portal:" "Green"
        $headers = @{ "X-API-Key" = $PortalApiKey }
        try {
            $agents = Invoke-RestMethod -Uri "$PortalApiUrl/api/agents" -Headers $headers
            $agents | Format-Table -Property Name, Status, Location, Environment, ActiveJobs
        } catch {
            Write-ColorOutput "Could not fetch agents from Portal API" "Yellow"
        }
    }
}

# Stop all agents
function Stop-AllAgents {
    Write-ColorOutput "Stopping all agents..." "Yellow"
    
    # Stop docker-compose services
    docker-compose down
    
    # Stop individual containers
    docker ps -q --filter "name=obfuscation-agent" | ForEach-Object {
        docker stop $_
        docker rm $_
    }
    
    Write-ColorOutput "All agents stopped" "Green"
}

# View agent logs
function Show-AgentLogs {
    param([string]$AgentName = "obfuscation-agent-sql")
    
    Write-ColorOutput "Viewing logs for $AgentName..." "Yellow"
    docker logs -f $AgentName
}

# Quick deploy function for easy setup
function Quick-Deploy {
    Write-ColorOutput "`n=== QUICK DEPLOY - OBFUSCATION AGENTS ===" "Cyan"
    Write-ColorOutput "This will deploy 2 on-prem agents and 7 Azure agents" "Cyan"
    
    # Get configuration
    if (-not $PortalApiUrl) {
        $PortalApiUrl = Read-Host "`nEnter your Portal API URL (e.g., http://YOUR_IP:7001)"
    }
    
    if (-not $PortalApiKey) {
        $secureKey = Read-Host "Enter Portal API Key" -AsSecureString
        $PortalApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        )
    }
    
    $deployAzure = Read-Host "`nDeploy to Azure? (Y/N)"
    
    # Build image
    Build-AgentImage
    
    # Deploy on-premise agents
    Deploy-OnPremiseAgents
    
    # Deploy to Azure if requested
    if ($deployAzure -eq 'Y') {
        Test-Prerequisites -CheckAzure $true
        
        $DockerRegistry = Read-Host "Enter your Azure Container Registry name (e.g., youracr.azurecr.io)"
        
        # Login to ACR
        Write-ColorOutput "Logging into Azure Container Registry..." "Yellow"
        az acr login --name ($DockerRegistry -replace '\.azurecr\.io', '')
        
        # Push image
        Push-AgentImage
        
        # Deploy to Azure
        Deploy-AzureAgents
    }
    
    Write-ColorOutput "`n=== DEPLOYMENT COMPLETE ===" "Green"
    Show-AgentStatus
}

# Show menu
function Show-Menu {
    Clear-Host
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "   Obfuscation Agent Deployment Tool" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "1. Quick Deploy (Recommended)"
    Write-Host "2. Build Docker Image"
    Write-Host "3. Deploy On-Premise Agents (2 agents)"
    Write-Host "4. Deploy Azure Agents (7 agents)"
    Write-Host "5. Deploy Single Test Agent"
    Write-Host "6. Show Agent Status"
    Write-Host "7. View Agent Logs"
    Write-Host "8. Stop All Agents"
    Write-Host "9. Push Image to Registry"
    Write-Host "0. Exit"
    Write-Host "======================================" -ForegroundColor Cyan
}

# Main execution
switch ($Action) {
    "Menu" {
        do {
            Show-Menu
            $choice = Read-Host "Select an option"
            
            switch ($choice) {
                "1" { Quick-Deploy }
                "2" { 
                    Test-Prerequisites
                    Build-AgentImage
                }
                "3" {
                    Test-Prerequisites
                    Deploy-OnPremiseAgents
                }
                "4" {
                    Test-Prerequisites -CheckAzure $true
                    Deploy-AzureAgents
                }
                "5" {
                    Test-Prerequisites
                    $name = Read-Host "Enter agent name (default: test-agent)"
                    if (-not $name) { $name = "test-agent" }
                    Deploy-SingleAgent -AgentName $name
                }
                "6" { Show-AgentStatus }
                "7" {
                    $name = Read-Host "Enter agent name (default: obfuscation-agent-sql)"
                    if (-not $name) { $name = "obfuscation-agent-sql" }
                    Show-AgentLogs -AgentName $name
                }
                "8" { Stop-AllAgents }
                "9" { Push-AgentImage }
                "0" {
                    Write-ColorOutput "Goodbye!" "Green"
                    exit 0
                }
                default { Write-ColorOutput "Invalid option" "Red" }
            }
            
            if ($choice -ne "0") {
                Write-Host "`nPress Enter to continue..."
                Read-Host
            }
        } while ($choice -ne "0")
    }
    "QuickDeploy" { Quick-Deploy }
    "Build" {
        Test-Prerequisites
        Build-AgentImage
    }
    "DeployOnPrem" {
        Test-Prerequisites
        Deploy-OnPremiseAgents
    }
    "DeployAzure" {
        Test-Prerequisites -CheckAzure $true
        Deploy-AzureAgents
    }
    "Status" { Show-AgentStatus }
    "Stop" { Stop-AllAgents }
    default {
        Write-ColorOutput "Unknown action: $Action" "Red"
        Write-Host "Valid actions: Menu, QuickDeploy, Build, DeployOnPrem, DeployAzure, Status, Stop"
    }
}
#!/bin/bash

# Cloud-Ready Agent Deployment Script
# Supports Docker, Azure App Service, and Kubernetes deployments

set -e

# Configuration
PORTAL_API_URL="${PORTAL_API_URL:-http://localhost:7001}"
AGENT_IMAGE="${AGENT_IMAGE:-obfuscation-agent:latest}"
REGISTRY="${REGISTRY:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_message() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to pre-register an agent
pre_register_agent() {
    local agent_name=$1
    local hostname=$2
    local location=$3
    local environment=${4:-Production}
    
    print_message "$YELLOW" "Pre-registering agent: $agent_name"
    
    # Create registration request
    local request_body=$(cat <<EOF
{
    "name": "$agent_name",
    "hostName": "$hostname",
    "port": 8080,
    "version": "1.0.0",
    "environment": "$environment",
    "location": "$location",
    "cpuCores": 2,
    "memoryMB": 2048,
    "maxConcurrentJobs": 2,
    "capabilities": {
        "canProcessSqlServer": true,
        "canProcessMySQL": false,
        "canProcessPostgreSQL": false,
        "canProcessOracle": false,
        "maxDatabaseSizeGB": 100,
        "maxRowsPerBatch": 10000
    }
}
EOF
)
    
    # Call API to pre-register
    response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -d "$request_body" \
        "$PORTAL_API_URL/api/agent/pre-register")
    
    # Extract agent ID from response
    agent_id=$(echo $response | grep -o '"agentId":"[^"]*' | cut -d'"' -f4)
    api_key=$(echo $response | grep -o '"apiKey":"[^"]*' | cut -d'"' -f4)
    
    if [ -z "$agent_id" ]; then
        print_message "$RED" "Failed to pre-register agent. Response: $response"
        return 1
    fi
    
    print_message "$GREEN" "Agent pre-registered successfully!"
    print_message "$BLUE" "Agent ID: $agent_id"
    print_message "$BLUE" "API Key: ${api_key:0:20}..."
    
    # Return the agent details
    echo "$agent_id|$api_key"
}

# Function to generate Docker run command
generate_docker_command() {
    local agent_name=$1
    local agent_id=$2
    local api_key=$3
    
    print_message "$YELLOW" "\nDocker deployment command:"
    
    cat <<EOF
docker run -d \\
    --name $agent_name \\
    --restart unless-stopped \\
    -e "OBFUSCATION_Agent__AgentId=$agent_id" \\
    -e "OBFUSCATION_Agent__Name=$agent_name" \\
    -e "OBFUSCATION_Portal__BaseUrl=$PORTAL_API_URL" \\
    -e "OBFUSCATION_Portal__RegistrationEndpoint=/api/agent/register" \\
    $AGENT_IMAGE
EOF
}

# Function to generate Kubernetes YAML
generate_kubernetes_yaml() {
    local agent_name=$1
    local agent_id=$2
    local api_key=$3
    local namespace=${4:-default}
    
    print_message "$YELLOW" "\nKubernetes deployment YAML:"
    
    cat <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: $agent_name
  namespace: $namespace
spec:
  replicas: 1
  selector:
    matchLabels:
      app: obfuscation-agent
      agent: $agent_name
  template:
    metadata:
      labels:
        app: obfuscation-agent
        agent: $agent_name
    spec:
      containers:
      - name: agent
        image: $AGENT_IMAGE
        env:
        - name: OBFUSCATION_Agent__AgentId
          value: "$agent_id"
        - name: OBFUSCATION_Agent__Name
          value: "$agent_name"
        - name: OBFUSCATION_Portal__BaseUrl
          value: "$PORTAL_API_URL"
        - name: OBFUSCATION_Portal__RegistrationEndpoint
          value: "/api/agent/register"
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1"
EOF
}

# Function to generate Azure App Service deployment
generate_azure_command() {
    local agent_name=$1
    local agent_id=$2
    local api_key=$3
    local resource_group=${4:-obfuscation-rg}
    local location=${5:-eastus}
    
    print_message "$YELLOW" "\nAzure App Service deployment commands:"
    
    cat <<EOF
# Create App Service Plan (if not exists)
az appservice plan create \\
    --name obfuscation-plan \\
    --resource-group $resource_group \\
    --sku B1 \\
    --is-linux

# Create Web App for Containers
az webapp create \\
    --name $agent_name \\
    --resource-group $resource_group \\
    --plan obfuscation-plan \\
    --deployment-container-image-name $AGENT_IMAGE

# Configure environment variables
az webapp config appsettings set \\
    --name $agent_name \\
    --resource-group $resource_group \\
    --settings \\
    OBFUSCATION_Agent__AgentId=$agent_id \\
    OBFUSCATION_Agent__Name=$agent_name \\
    OBFUSCATION_Portal__BaseUrl=$PORTAL_API_URL \\
    OBFUSCATION_Portal__RegistrationEndpoint=/api/agent/register
EOF
}

# Main menu
show_menu() {
    echo "======================================"
    echo "   Cloud-Ready Agent Deployment"
    echo "======================================"
    echo "1. Pre-register and Deploy (Docker)"
    echo "2. Pre-register and Deploy (Kubernetes)"
    echo "3. Pre-register and Deploy (Azure App Service)"
    echo "4. Pre-register Only (Get Agent ID)"
    echo "5. Generate Deployment Commands (Existing Agent)"
    echo "0. Exit"
    echo "======================================"
}

# Main script
main() {
    while true; do
        show_menu
        read -p "Select an option: " choice
        
        case $choice in
            1)
                # Docker deployment
                read -p "Enter agent name: " agent_name
                read -p "Enter hostname (default: docker-host): " hostname
                hostname=${hostname:-docker-host}
                read -p "Enter location (default: Docker): " location
                location=${location:-Docker}
                
                # Pre-register agent
                result=$(pre_register_agent "$agent_name" "$hostname" "$location")
                if [ $? -eq 0 ]; then
                    IFS='|' read -r agent_id api_key <<< "$result"
                    
                    # Generate Docker command
                    generate_docker_command "$agent_name" "$agent_id" "$api_key"
                    
                    echo
                    read -p "Run this command now? (y/n): " run_now
                    if [ "$run_now" = "y" ]; then
                        eval $(generate_docker_command "$agent_name" "$agent_id" "$api_key")
                        print_message "$GREEN" "Agent deployed successfully!"
                    fi
                fi
                ;;
                
            2)
                # Kubernetes deployment
                read -p "Enter agent name: " agent_name
                read -p "Enter namespace (default: default): " namespace
                namespace=${namespace:-default}
                read -p "Enter location (default: Kubernetes): " location
                location=${location:-Kubernetes}
                
                # Pre-register agent
                result=$(pre_register_agent "$agent_name" "k8s-node" "$location")
                if [ $? -eq 0 ]; then
                    IFS='|' read -r agent_id api_key <<< "$result"
                    
                    # Generate Kubernetes YAML
                    yaml_file="${agent_name}-deployment.yaml"
                    generate_kubernetes_yaml "$agent_name" "$agent_id" "$api_key" "$namespace" > $yaml_file
                    
                    print_message "$GREEN" "Kubernetes YAML saved to: $yaml_file"
                    echo
                    echo "Deploy with: kubectl apply -f $yaml_file"
                fi
                ;;
                
            3)
                # Azure App Service deployment
                read -p "Enter agent name: " agent_name
                read -p "Enter resource group: " resource_group
                read -p "Enter location (default: eastus): " location
                location=${location:-eastus}
                
                # Pre-register agent
                result=$(pre_register_agent "$agent_name" "azure-app-service" "Azure-$location")
                if [ $? -eq 0 ]; then
                    IFS='|' read -r agent_id api_key <<< "$result"
                    
                    # Generate Azure commands
                    generate_azure_command "$agent_name" "$agent_id" "$api_key" "$resource_group" "$location"
                fi
                ;;
                
            4)
                # Pre-register only
                read -p "Enter agent name: " agent_name
                read -p "Enter hostname: " hostname
                read -p "Enter location: " location
                read -p "Enter environment (default: Production): " environment
                environment=${environment:-Production}
                
                # Pre-register agent
                result=$(pre_register_agent "$agent_name" "$hostname" "$location" "$environment")
                if [ $? -eq 0 ]; then
                    IFS='|' read -r agent_id api_key <<< "$result"
                    
                    print_message "$GREEN" "\nAgent pre-registered successfully!"
                    print_message "$BLUE" "Save these values for deployment:"
                    echo "AGENT_ID=$agent_id"
                    echo "API_KEY=$api_key"
                fi
                ;;
                
            5)
                # Generate commands for existing agent
                read -p "Enter agent name: " agent_name
                read -p "Enter existing agent ID: " agent_id
                read -p "Enter API key (optional): " api_key
                
                echo
                print_message "$YELLOW" "Select deployment target:"
                echo "1. Docker"
                echo "2. Kubernetes"
                echo "3. Azure App Service"
                read -p "Choice: " target
                
                case $target in
                    1)
                        generate_docker_command "$agent_name" "$agent_id" "$api_key"
                        ;;
                    2)
                        read -p "Enter namespace (default: default): " namespace
                        namespace=${namespace:-default}
                        generate_kubernetes_yaml "$agent_name" "$agent_id" "$api_key" "$namespace"
                        ;;
                    3)
                        read -p "Enter resource group: " resource_group
                        read -p "Enter location (default: eastus): " location
                        location=${location:-eastus}
                        generate_azure_command "$agent_name" "$agent_id" "$api_key" "$resource_group" "$location"
                        ;;
                esac
                ;;
                
            0)
                print_message "$GREEN" "Goodbye!"
                exit 0
                ;;
                
            *)
                print_message "$RED" "Invalid option"
                ;;
        esac
        
        echo
        read -p "Press Enter to continue..."
    done
}

# Check if running with arguments
if [ $# -gt 0 ]; then
    case "$1" in
        pre-register)
            # Direct pre-registration
            shift
            if [ $# -lt 3 ]; then
                echo "Usage: $0 pre-register <name> <hostname> <location> [environment]"
                exit 1
            fi
            pre_register_agent "$@"
            ;;
        help)
            echo "Usage: $0 [command] [options]"
            echo ""
            echo "Commands:"
            echo "  pre-register <name> <hostname> <location> [environment]"
            echo "      Pre-register an agent and get its ID"
            echo ""
            echo "Environment variables:"
            echo "  PORTAL_API_URL    Portal API URL (default: http://localhost:7001)"
            echo "  AGENT_IMAGE       Docker image name (default: obfuscation-agent:latest)"
            echo "  REGISTRY          Docker registry (optional)"
            ;;
        *)
            echo "Unknown command: $1"
            echo "Use '$0 help' for usage information"
            exit 1
            ;;
    esac
else
    # Interactive mode
    main
fi
#!/bin/bash

# Obfuscation Agent Deployment Script
# Supports both on-premise and Azure deployments

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
DOCKER_REGISTRY="${DOCKER_REGISTRY:-youracr.azurecr.io}"
IMAGE_NAME="obfuscation-agent"
IMAGE_TAG="${IMAGE_TAG:-latest}"
PORTAL_API_URL="${PORTAL_API_URL}"
PORTAL_API_KEY="${PORTAL_API_KEY}"

# Function to print colored output
print_message() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to check prerequisites
check_prerequisites() {
    print_message "$YELLOW" "Checking prerequisites..."
    
    # Check Docker
    if ! command -v docker &> /dev/null; then
        print_message "$RED" "Docker is not installed. Please install Docker first."
        exit 1
    fi
    
    # Check Azure CLI for Azure deployments
    if [[ "$1" == "azure" ]]; then
        if ! command -v az &> /dev/null; then
            print_message "$RED" "Azure CLI is not installed. Please install Azure CLI first."
            exit 1
        fi
    fi
    
    print_message "$GREEN" "Prerequisites check passed!"
}

# Function to build Docker image
build_image() {
    print_message "$YELLOW" "Building Docker image..."
    
    cd "$(dirname "$0")"
    docker build -t ${IMAGE_NAME}:${IMAGE_TAG} .
    
    if [ $? -eq 0 ]; then
        print_message "$GREEN" "Docker image built successfully!"
    else
        print_message "$RED" "Failed to build Docker image"
        exit 1
    fi
}

# Function to push image to registry
push_image() {
    print_message "$YELLOW" "Pushing image to registry..."
    
    # Tag for registry
    docker tag ${IMAGE_NAME}:${IMAGE_TAG} ${DOCKER_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
    
    # Push to registry
    docker push ${DOCKER_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
    
    if [ $? -eq 0 ]; then
        print_message "$GREEN" "Image pushed to registry successfully!"
    else
        print_message "$RED" "Failed to push image to registry"
        exit 1
    fi
}

# Function to deploy on-premise agents
deploy_onprem() {
    print_message "$YELLOW" "Deploying on-premise agents..."
    
    # Check if Portal API URL is set
    if [ -z "$PORTAL_API_URL" ]; then
        read -p "Enter Portal API URL (e.g., http://192.168.1.100:7001): " PORTAL_API_URL
    fi
    
    if [ -z "$PORTAL_API_KEY" ]; then
        read -s -p "Enter Portal API Key: " PORTAL_API_KEY
        echo
    fi
    
    # Export environment variables for docker-compose
    export PORTAL_API_URL
    export PORTAL_API_KEY
    
    # Deploy using docker-compose
    docker-compose up -d
    
    if [ $? -eq 0 ]; then
        print_message "$GREEN" "On-premise agents deployed successfully!"
        docker-compose ps
    else
        print_message "$RED" "Failed to deploy on-premise agents"
        exit 1
    fi
}

# Function to deploy Azure agents
deploy_azure() {
    local agent_count=${1:-7}
    local resource_group=${2:-"obfuscation-agents-rg"}
    local location=${3:-"eastus"}
    
    print_message "$YELLOW" "Deploying $agent_count agents to Azure..."
    
    # Login to Azure if not already logged in
    if ! az account show &> /dev/null; then
        print_message "$YELLOW" "Please login to Azure..."
        az login
    fi
    
    # Create resource group if it doesn't exist
    if ! az group show --name $resource_group &> /dev/null; then
        print_message "$YELLOW" "Creating resource group: $resource_group"
        az group create --name $resource_group --location $location
    fi
    
    # Deploy using ARM template
    print_message "$YELLOW" "Deploying Azure Container Instances..."
    
    az deployment group create \
        --resource-group $resource_group \
        --template-file azure-deploy/azure-container-instances.json \
        --parameters \
            agentCount=$agent_count \
            containerImage="${DOCKER_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}" \
            portalApiUrl="$PORTAL_API_URL" \
            portalApiKey="$PORTAL_API_KEY" \
            location=$location
    
    if [ $? -eq 0 ]; then
        print_message "$GREEN" "Azure agents deployed successfully!"
        
        # List deployed container instances
        az container list --resource-group $resource_group --output table
    else
        print_message "$RED" "Failed to deploy Azure agents"
        exit 1
    fi
}

# Function to deploy using Kubernetes
deploy_kubernetes() {
    local namespace=${1:-"obfuscation"}
    local replicas=${2:-5}
    
    print_message "$YELLOW" "Deploying to Kubernetes..."
    
    # Check kubectl
    if ! command -v kubectl &> /dev/null; then
        print_message "$RED" "kubectl is not installed. Please install kubectl first."
        exit 1
    fi
    
    # Create namespace if it doesn't exist
    kubectl create namespace $namespace --dry-run=client -o yaml | kubectl apply -f -
    
    # Apply Kubernetes manifests
    kubectl apply -f kubernetes/ -n $namespace
    
    # Scale deployment
    kubectl scale deployment obfuscation-agent --replicas=$replicas -n $namespace
    
    if [ $? -eq 0 ]; then
        print_message "$GREEN" "Kubernetes deployment successful!"
        kubectl get pods -n $namespace
    else
        print_message "$RED" "Failed to deploy to Kubernetes"
        exit 1
    fi
}

# Function to show agent status
show_status() {
    print_message "$YELLOW" "Checking agent status..."
    
    # Check local Docker containers
    print_message "$GREEN" "Local Docker Agents:"
    docker ps --filter "name=obfuscation-agent" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    
    # Check Azure agents if logged in
    if az account show &> /dev/null 2>&1; then
        print_message "$GREEN" "\nAzure Container Instances:"
        az container list --query "[?contains(name, 'obfuscation-agent')].{Name:name, Status:instanceView.state, Location:location}" --output table
    fi
    
    # Check Kubernetes if available
    if command -v kubectl &> /dev/null && kubectl cluster-info &> /dev/null 2>&1; then
        print_message "$GREEN" "\nKubernetes Pods:"
        kubectl get pods -l app=obfuscation-agent --all-namespaces
    fi
}

# Function to stop all agents
stop_agents() {
    print_message "$YELLOW" "Stopping agents..."
    
    # Stop local Docker containers
    docker-compose down
    
    print_message "$GREEN" "Agents stopped"
}

# Function to view logs
view_logs() {
    local agent_name=${1:-"obfuscation-agent-sql"}
    
    print_message "$YELLOW" "Viewing logs for $agent_name..."
    docker logs -f $agent_name
}

# Main menu
show_menu() {
    echo "======================================"
    echo "   Obfuscation Agent Deployment Tool"
    echo "======================================"
    echo "1. Build Docker Image"
    echo "2. Deploy On-Premise Agents (2 agents)"
    echo "3. Deploy Azure Agents (7 agents)"
    echo "4. Deploy to Kubernetes"
    echo "5. Deploy All (On-Prem + Azure)"
    echo "6. Show Agent Status"
    echo "7. View Agent Logs"
    echo "8. Stop All Agents"
    echo "9. Push Image to Registry"
    echo "0. Exit"
    echo "======================================"
}

# Main script
main() {
    if [ $# -eq 0 ]; then
        # Interactive mode
        while true; do
            show_menu
            read -p "Select an option: " choice
            
            case $choice in
                1)
                    check_prerequisites
                    build_image
                    ;;
                2)
                    check_prerequisites
                    deploy_onprem
                    ;;
                3)
                    check_prerequisites "azure"
                    read -p "Enter number of Azure agents (default: 7): " count
                    count=${count:-7}
                    deploy_azure $count
                    ;;
                4)
                    check_prerequisites
                    read -p "Enter number of replicas (default: 5): " replicas
                    replicas=${replicas:-5}
                    deploy_kubernetes "obfuscation" $replicas
                    ;;
                5)
                    check_prerequisites "azure"
                    build_image
                    push_image
                    deploy_onprem
                    deploy_azure 7
                    ;;
                6)
                    show_status
                    ;;
                7)
                    read -p "Enter agent name (default: obfuscation-agent-sql): " name
                    name=${name:-obfuscation-agent-sql}
                    view_logs $name
                    ;;
                8)
                    stop_agents
                    ;;
                9)
                    push_image
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
    else
        # Command line mode
        case "$1" in
            build)
                check_prerequisites
                build_image
                ;;
            deploy-onprem)
                check_prerequisites
                deploy_onprem
                ;;
            deploy-azure)
                check_prerequisites "azure"
                deploy_azure ${2:-7} ${3:-"obfuscation-agents-rg"} ${4:-"eastus"}
                ;;
            deploy-k8s)
                check_prerequisites
                deploy_kubernetes ${2:-"obfuscation"} ${3:-5}
                ;;
            deploy-all)
                check_prerequisites "azure"
                build_image
                push_image
                deploy_onprem
                deploy_azure 7
                ;;
            status)
                show_status
                ;;
            logs)
                view_logs ${2:-"obfuscation-agent-sql"}
                ;;
            stop)
                stop_agents
                ;;
            help)
                echo "Usage: $0 [command] [options]"
                echo ""
                echo "Commands:"
                echo "  build              Build Docker image"
                echo "  deploy-onprem      Deploy on-premise agents"
                echo "  deploy-azure       Deploy Azure agents"
                echo "  deploy-k8s         Deploy to Kubernetes"
                echo "  deploy-all         Deploy all agents"
                echo "  status             Show agent status"
                echo "  logs [name]        View agent logs"
                echo "  stop               Stop all agents"
                echo "  help               Show this help message"
                ;;
            *)
                print_message "$RED" "Unknown command: $1"
                echo "Use '$0 help' for usage information"
                exit 1
                ;;
        esac
    fi
}

# Run main function
main "$@"
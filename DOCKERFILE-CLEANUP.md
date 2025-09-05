# Dockerfile Cleanup Analysis

## Agent Dockerfiles (portal/agent-client/)

### KEEP: Dockerfile (Main)
- **Status**: Complete and well-structured
- **Purpose**: Primary production Dockerfile for agents
- **Features**:
  - Multi-stage build (optimized image size)
  - Installs SQL Server and PostgreSQL tools
  - Creates non-root user for security
  - Health check endpoint
  - Properly handles contracts dependency

### KEEP: Dockerfile.full
- **Status**: Alternative complete version
- **Purpose**: Full build with all dependencies
- **Features**:
  - Builds contracts from source
  - Simpler than main but still complete
  - Good for development/testing

### REMOVE: Dockerfile.simple
- **Status**: Incomplete/Not production-ready
- **Issues**:
  - Uses `dotnet run` instead of compiled binaries (slow startup)
  - No multi-stage build (large image)
  - Missing security features
  - Not suitable for production

### REMOVE: Dockerfile.agent  
- **Status**: Incomplete
- **Issues**:
  - References non-existent "agent-publish/" directory
  - No build stage
  - Would require manual pre-compilation

## Portal Dockerfiles

### KEEP: portal/api/Dockerfile
- **Status**: Complete
- **Purpose**: API service container
- Multi-stage build, proper dependency handling

### KEEP: portal/web/Dockerfile
- **Status**: Complete (assumed based on pattern)
- **Purpose**: Web UI container

### REMOVE: templates/Dockerfile.template
- **Status**: Template file
- Not needed in production code

## Recommendation

### Files to Keep:
1. `portal/agent-client/Dockerfile` - Primary agent Dockerfile
2. `portal/agent-client/Dockerfile.full` - Alternative full build
3. `portal/api/Dockerfile` - API service
4. `portal/web/Dockerfile` - Web UI

### Files to Remove:
1. `portal/agent-client/Dockerfile.simple` - Incomplete, not production-ready
2. `portal/agent-client/Dockerfile.agent` - References non-existent files
3. `templates/Dockerfile.template` - Template file

## Docker Image Build Commands

### For Agents (from portal/agent-client/):
```bash
# Using main Dockerfile
docker build -t obfuscation-agent:latest -f Dockerfile .

# Using full Dockerfile (if contracts changed)
docker build -t obfuscation-agent:latest -f Dockerfile.full ..
```

### For API (from portal/):
```bash
docker build -t portal-api:latest -f api/Dockerfile .
```

### For Web (from portal/):
```bash
docker build -t portal-web:latest -f web/Dockerfile .
```
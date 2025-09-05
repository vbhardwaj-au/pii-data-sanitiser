# Multi-stage build for Obfuscation Agent
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Common project (needed by data-obfuscation)
COPY Common/Common.csproj ./Common/
COPY Common/ ./Common/

# Copy data-obfuscation project
COPY data-obfuscation/DataObfuscation.csproj ./data-obfuscation/
COPY data-obfuscation/ ./data-obfuscation/

# Copy contracts project first
COPY portal/contracts/Contracts.csproj ./contracts/
COPY portal/contracts/ ./contracts/

# Copy agent project file
COPY portal/agent-client/ObfuscationAgent/ObfuscationAgent.csproj ./ObfuscationAgent/

# Copy agent source code
COPY portal/agent-client/ObfuscationAgent/ ./ObfuscationAgent/

# Fix the project reference path for Docker build context - handle both Windows and Unix paths
RUN sed -i 's|<ProjectReference Include="[^"]*Contracts.csproj" />|<ProjectReference Include="../contracts/Contracts.csproj" />|g' ./ObfuscationAgent/ObfuscationAgent.csproj

# Build data-obfuscation project
WORKDIR /src/data-obfuscation
RUN dotnet restore DataObfuscation.csproj
RUN dotnet publish -c Release -o /app/data-obfuscation

# Restore dependencies for agent
WORKDIR /src
RUN dotnet restore ObfuscationAgent/ObfuscationAgent.csproj

# Build
WORKDIR /src/ObfuscationAgent
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQL Server tools for different database types
RUN apt-get update && \
    apt-get install -y \
    curl \
    gnupg \
    apt-transport-https \
    postgresql-client \
    && curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - \
    && curl https://packages.microsoft.com/config/debian/11/prod.list > /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y msodbcsql18 mssql-tools18 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Add SQL tools to PATH
ENV PATH="$PATH:/opt/mssql-tools18/bin"

# Copy published app
COPY --from=publish /app/publish .

# Copy data-obfuscation build
COPY --from=build /app/data-obfuscation ./data-obfuscation

# Remove the executable file (keep only DLL)
RUN rm -f /app/ObfuscationAgent

# Create non-root user and data directories
RUN groupadd -r obfuscator && useradd -r -g obfuscator obfuscator && \
    mkdir -p /home/obfuscator/.local/share/ObfuscationAgent && \
    mkdir -p /app/checkpoints && \
    mkdir -p /app/logs && \
    mkdir -p /app/reports && \
    chown -R obfuscator:obfuscator /home/obfuscator && \
    chown -R obfuscator:obfuscator /app/checkpoints && \
    chown -R obfuscator:obfuscator /app/logs && \
    chown -R obfuscator:obfuscator /app/reports

# Set user
USER obfuscator

# Set HOME environment variable for proper LocalApplicationData resolution
ENV HOME=/home/obfuscator

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Entry point
ENTRYPOINT ["dotnet", "/app/ObfuscationAgent.dll"]
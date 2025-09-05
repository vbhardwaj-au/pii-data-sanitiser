# PII Obfuscation System - Project Architecture

## Solution Structure Overview

The solution consists of two main parts:
1. **Original Core Obfuscation Tools** (Standalone)
2. **Portal & Distributed Agent System** (New Web-based Management)

## Core Obfuscation Projects (Original - Still Used)

### 1. auto-mapping-generator
- **Purpose**: PII Column Discovery and Configuration Generation
- **Role**: Analyzes database schemas to automatically identify PII columns
- **Features**:
  - Pattern-based PII detection
  - LLM-enhanced detection (optional)
  - Generates unified JSON configuration files
- **Usage**: Standalone CLI tool - runs independently
- **Output**: JSON mapping configuration files

### 2. data-obfuscation  
- **Purpose**: The Actual Data Obfuscation Engine
- **Role**: Performs the actual data transformation/obfuscation
- **Current Status**: **STILL ACTIVELY USED** by the distributed agents
- **Features**:
  - Deterministic obfuscation algorithms
  - Australian data generation (addresses, phones, ABNs)
  - High-performance batch processing
  - Caching for performance
- **How it's used**:
  - Agents execute this as a subprocess (DataObfuscation.exe)
  - Agents pass mapping configs and connection strings to it
  - It runs as a separate process for isolation and scalability

### 3. Common
- **Purpose**: Shared libraries and models
- **Role**: Common code used by both auto-mapping-generator and data-obfuscation
- **Contains**: Shared data models, utilities, constants

## Portal System Projects (New Web Management Layer)

### Portal Core Projects (in portal/ folder)

#### 1. portal/api (API.csproj)
- **Purpose**: RESTful API Backend
- **Role**: Central management and orchestration
- **Features**:
  - Product management
  - Connection string management (with encryption)
  - Pipeline orchestration
  - Agent management and job distribution
  - Refresh schema functionality
  - Authentication via API keys
- **Uses**: MediatR pattern, Entity Framework Core

#### 2. portal/web (Web.csproj)
- **Purpose**: Web UI Frontend
- **Role**: User interface for managing obfuscation operations
- **Features**:
  - Product configuration UI
  - Schema mapping visualization
  - Pipeline monitoring
  - Agent status dashboard
  - Connection string management UI
- **Technology**: ASP.NET Core MVC with Razor views

#### 3. portal/contracts (Contracts.csproj)
- **Purpose**: Shared DTOs and Models
- **Role**: Contract definitions between API, Web, and Agents
- **Contains**: 
  - Data Transfer Objects (DTOs)
  - Entity models
  - Shared constants and enums

#### 4. portal/dal (DAL.csproj)
- **Purpose**: Data Access Layer
- **Role**: Database operations and Entity Framework repositories
- **Features**:
  - Repository pattern implementation
  - Entity Framework DbContext
  - Database migrations
- **Database**: SQL Server (Azure)

### Distributed Agent Projects

#### 5. portal/agent-client/ObfuscationAgent
- **Purpose**: Distributed Agent for Executing Obfuscation Jobs
- **Role**: Worker that pulls jobs from portal and executes them
- **Key Features**:
  - Polls portal API for jobs
  - **Executes data-obfuscation project as subprocess**
  - Reports progress back to portal
  - Heartbeat mechanism for health monitoring
  - Can run in Docker containers for scalability
- **Important**: This agent doesn't contain obfuscation logic itself - it orchestrates the data-obfuscation executable

## Project Dependencies and Relationships

```
┌─────────────────────────────────────────────────────────┐
│                   User Interface                         │
│                   portal/web                             │
└────────────────────────┬─────────────────────────────────┘
                         │ HTTP/REST
┌────────────────────────▼─────────────────────────────────┐
│                   Portal API                             │
│                   portal/api                             │
│  (Orchestration, Job Management, Agent Coordination)     │
└──────┬──────────────────┬─────────────────────┬──────────┘
       │                  │                      │
       │ Uses             │ HTTP/REST           │ Uses
       │                  │                      │
┌──────▼──────┐    ┌──────▼──────┐      ┌───────▼────────┐
│  portal/dal │    │ObfuscationAgent│    │portal/contracts│
│  (Database) │    │   (Workers)    │    │    (DTOs)     │
└─────────────┘    └──────┬──────────┘    └────────────────┘
                          │
                          │ Executes as subprocess
                          │
                   ┌──────▼──────────┐
                   │data-obfuscation │
                   │ (Core Engine)   │
                   └─────────────────┘
```

## Current Role of data-obfuscation Project

The **data-obfuscation** project remains **CRITICAL** in the current architecture:

1. **Not Replaced**: It hasn't been replaced by the portal system
2. **Core Engine**: It's still the actual obfuscation engine
3. **Execution Model**: 
   - Agents receive jobs from the portal
   - Agents execute data-obfuscation.exe as a subprocess
   - Agents pass configuration and monitor progress
   - Results are reported back to portal

4. **Why Separate Process**:
   - Isolation: Crashes don't affect the agent
   - Scalability: Multiple instances can run
   - Resource management: Separate memory/CPU allocation
   - Compatibility: Can be updated independently

## Workflow

1. **Configuration Phase**:
   - User runs `auto-mapping-generator` to discover PII columns
   - Generates mapping configuration JSON

2. **Portal Management**:
   - User uploads configuration to portal
   - Configures connection strings
   - Sets up pipeline stages

3. **Execution Phase**:
   - Portal creates jobs
   - Agents poll for jobs
   - Agents execute `data-obfuscation` with job parameters
   - Progress reported back to portal
   - Results stored and displayed

## Summary

- **auto-mapping-generator**: PII Discovery (standalone CLI)
- **data-obfuscation**: Core obfuscation engine (still actively used)
- **Portal System**: Web-based management layer
- **Agents**: Distributed workers that execute data-obfuscation
- **The data-obfuscation project is NOT obsolete** - it's the actual engine doing the work
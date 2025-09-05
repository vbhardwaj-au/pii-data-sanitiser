# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# PII Data Sanitizer - AI Assistant Context

## Project Overview
This is a high-performance data obfuscation solution for SQL Server databases, designed for cross-database deterministic obfuscation with a focus on Australian fleet management industry data.

## Key Requirements
1. **Deterministic Obfuscation**: Same input MUST always produce same output across all databases
2. **Cross-Database Consistency**: Values must match across different databases for referential integrity
3. **Australian Data Focus**: Generate realistic Australian addresses, phone numbers, ABNs, etc.
4. **High Performance**: Handle 1TB+ databases with millions of rows efficiently

## Architecture Summary
This solution consists of four main components:

### Core Components
- **Auto Mapping Generator** (`auto-mapping-generator/`): Discovers PII columns automatically using pattern matching and LLM-based analysis
- **Data Obfuscation Engine** (`data-obfuscation/`): Performs the actual data transformation with deterministic algorithms
- **Common Library** (`Common/`): Shared models, data types, and utilities
- **Web Portal** (`portal/`): Web-based management interface for configurations

### Portal Architecture (Vertical Slice Pattern)
- **API** (`portal/api/`): RESTful API with CQRS pattern using MediatR
- **Web UI** (`portal/web/`): ASP.NET Core MVC with Bootstrap frontend  
- **Contracts** (`portal/contracts/`): Shared DTOs and models
- **Data Access** (`portal/dal/`): Entity Framework Core repositories
- **Single Solution**: `portal/Portal.sln` contains all portal projects

### Key Architectural Principles
- **Deterministic Processing**: Same input always produces same output across databases
- **Selective Caching**: Only caches low-cardinality fields to manage memory
- **Vertical Slice Pattern**: Portal features are self-contained with CQRS
- **Unified Configuration**: Single JSON file contains all settings and mappings

## Critical Implementation Details

### Deterministic Generation
- Uses SHA-256 hashing with global seed + custom seed + original value
- Bogus library with deterministic Randomizer for consistent fake data
- Cumulative seeding for address components to avoid collisions

### Performance Optimizations
- Parallel processing with configurable threads (default: 8)
- Batch processing (default: 1000 rows)
- Selective caching (only ~5% of values cached)
- Connection pooling and command timeout handling

### Data Type Handling
- **Cached**: Names, cities, states, postcodes, company names
- **Not Cached**: Addresses, emails, phones, IDs, credit cards
- Length constraints enforced (e.g., City max 30 chars)

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Build individual components
dotnet build auto-mapping-generator/
dotnet build data-obfuscation/
dotnet build Common/

# Build portal (from portal directory)
cd portal && dotnet build Portal.sln
```

### Core Obfuscation Workflow
```bash
# 1. Analyze database schema and generate unified config
cd auto-mapping-generator
dotnet run "Server=localhost;Database=AdventureWorks;Integrated Security=true;"

# 2. Run obfuscation (always test with --dry-run first)
cd ../data-obfuscation
dotnet run ../JSON/AdventureWorks-mapping.json --dry-run

# 3. Run actual obfuscation
dotnet run ../JSON/AdventureWorks-mapping.json
```

### Portal Development
```bash
# Run API (from portal/api directory)
cd portal/api && dotnet run
# API runs on: https://localhost:7001, Swagger: /swagger

# Run Web UI (from portal/web directory) 
cd portal/web && dotnet run  
# Web runs on: https://localhost:7002

# Docker approach (from portal directory)
cd portal && docker-compose up -d
# Includes SQL Server, API, and Web UI
```

### Cache Management (Critical!)
```bash
# Clear obfuscation cache between runs to avoid duplicate key violations
rm -rf data-obfuscation/mappings/*/*.json

# Clear all generated configs
rm -rf JSON/*.json
```

## Known Issues & Solutions

### Duplicate Key Violations
- **Cause**: Cached values from previous runs
- **Solution**: Clear mapping cache before runs

### String Truncation Errors
- **Cause**: Generated values exceed column length
- **Solution**: Length constraints implemented in providers

### Memory Issues
- **Cause**: Too many cached values
- **Solution**: Selective caching implemented

## Testing Approach
1. Always use `--dry-run` first
2. Test with small batches initially
3. Monitor cache statistics in logs
4. Verify deterministic behavior across runs

## Web Portal Features
The portal provides a web-based interface for managing obfuscation configurations:

### Key Features
- **Product Management**: View database products and their obfuscation status
- **Mapping Visualization**: Detailed view of table and column mappings
- **Connection Testing**: Test database connectivity from the web interface
- **REST APIs**: Full API access for integration and automation
- **Docker Support**: Complete containerized deployment

### Portal URLs (when running locally)
- **Web Interface**: http://localhost:7002
- **API Swagger**: http://localhost:7001/swagger  
- **Database**: localhost:1433 (Docker: sa/YourStrong@Passw0rd)

## File Locations
- **Unified Configs**: `JSON/{database}-mapping.json` (contains all settings)
- **Legacy Configs**: `JSON/*.json` (deprecated separate files)
- **Logs**: `logs/obfuscation-*.log`
- **Reports**: `data-obfuscation/reports/*-obfuscation-*.json`
- **Cache**: `data-obfuscation/mappings/*/mappings_*.json`
- **Data Files**: `data-obfuscation/Data/AU/*.txt`
- **Portal Config**: `portal/api/appsettings.json`, `portal/web/appsettings.json`

## Performance Expectations
- **Small DB (< 1GB)**: ~2000 rows/sec
- **Medium DB (1-10GB)**: ~1800 rows/sec
- **Large DB (10-100GB)**: ~1500 rows/sec
- **Cache Hit Rate**: 80-90% for cached types

## When Modifying Code
1. Maintain deterministic behavior at all costs
2. Test with problematic addresses: "4022 H Pine Creek Way", "1522 Azalea Ave."
3. Ensure length constraints are respected
4. Clear cache after provider changes
5. Run in dry-run mode first

## Critical Files

### Core Obfuscation Engine
- `data-obfuscation/Data/DeterministicAustralianProvider.cs` - Main data generation logic
- `data-obfuscation/Configuration/CacheConfiguration.cs` - Selective caching rules
- `data-obfuscation/Core/ObfuscationEngine.cs` - Main processing engine
- `data-obfuscation/Core/UnifiedConfigurationParser.cs` - Unified config parsing
- `data-obfuscation/Program.cs` - Entry point with command-line handling

### Schema Analysis & Config Generation  
- `auto-mapping-generator/Core/ObfuscationConfigGenerator.cs` - Unified config generation
- `auto-mapping-generator/Services/EnhancedPIIDetectionService.cs` - PII detection logic
- `auto-mapping-generator/Program.cs` - Entry point with LLM integration

### Portal Architecture
- `portal/api/Features/Products/` - Product management vertical slices
- `portal/api/Program.cs` - API startup and DI configuration
- `portal/web/Controllers/HomeController.cs` - Main web interface controller
- `portal/web/Views/Home/` - Razor views for product and mapping visualization
- `portal/dal/Repositories/` - Data access layer with Entity Framework

### Shared Infrastructure
- `Common/Models/` - Shared data models and DTOs
- `portal/contracts/DTOs/` - API contract definitions

## Test Commands
```bash
# No formal test suite available - use dry-run mode for validation
cd data-obfuscation
dotnet run ../JSON/YourDB-mapping.json --dry-run --validate-only
```

## Development Workflow
```bash
# Full development workflow example
cd auto-mapping-generator
dotnet run "Server=localhost;Database=TestDB;Integrated Security=true;"

cd ../data-obfuscation  
dotnet run ../JSON/TestDB-mapping.json --dry-run
dotnet run ../JSON/TestDB-mapping.json

# Clear cache between iterations
rm -rf mappings/*/*.json
```

## Portal Development Workflow  
```bash
# Run all services with Docker
cd portal && docker-compose up -d

# Or run individually for development
cd portal/api && dotnet run    # API on https://localhost:7001
cd portal/web && dotnet run    # Web on https://localhost:7002

# API Documentation: https://localhost:7001/swagger
# Web Interface: https://localhost:7002
```

## LLM Integration Notes
- Auto-mapping-generator uses LLM services for enhanced PII detection
- Configure LLM providers in `auto-mapping-generator/appsettings.json`
- Supports multiple LLM providers through ILLMProviderFactory pattern
- Falls back to pattern-based detection if LLM services unavailable

# important-instruction-reminders
Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
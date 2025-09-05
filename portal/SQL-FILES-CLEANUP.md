# SQL Files Cleanup and Organization

## Main Schema File (KEEP)
- **portal/database-schema.sql** - Complete database schema (1085 lines)
  - Contains all tables, indexes, views, stored procedures
  - This is the authoritative schema file

## Files to REMOVE (Redundant/Migration Scripts)

### Migration Scripts (Already Applied)
These were one-time migration scripts that have already been applied:
- portal/database-scripts/01-check-current-schema.sql
- portal/database-scripts/02-check-pipeline-tables.sql
- portal/database-scripts/03-simple-table-check.sql
- portal/database-scripts/04-alter-pipeline-tables.sql
- portal/database-scripts/05-safe-alter-pipeline-tables.sql
- portal/database-scripts/06-simple-add-columns.sql
- portal/database-scripts/07-create-indexes-and-constraints.sql
- portal/database-scripts/08-seed-pipeline-stages.sql
- portal/database-scripts/09-check-pipeline-stages-structure.sql
- portal/database-scripts/10-correct-seed-pipeline-stages.sql
- portal/database-scripts/11-simple-seed-stages.sql
- portal/database-scripts/12-final-verification.sql
- portal/database-scripts/13-add-pipelinestages-columns.sql
- portal/database-scripts/14-add-productpipelineconfig-columns.sql
- portal/database-scripts/15-run-all-missing-migrations.sql
- portal/database-scripts/16-seed-pipeline-stages.sql
- portal/database-scripts/17-update-pipeline-stages.sql
- portal/database-scripts/18-create-pipelinescripts-table.sql
- portal/database-scripts/19-fix-pipelinescripts-columns.sql
- portal/database-scripts/20-recreate-pipelinescripts-table.sql

### Other Redundant Files
- portal/database/001-create-distributed-agent-tables.sql (already in main schema)
- portal/migrations/001-add-pipeline-tables.sql (already in main schema)

## Files to KEEP

### Main Schema
- **portal/database-schema.sql** - Complete database schema

### Seed Data (Optional - Keep for reference)
- **portal/seed.sql** - Sample seed data for testing

### Utility Scripts (Keep for operations)
- **portal/sql/reseed_mappings.sql** - Utility to reseed mappings
- **portal/sql/sync_supported_data_types.sql** - Utility to sync data types

### Documentation
- **portal/database-scripts/README-SCHEMA-MIGRATION.md** - Migration history documentation

## Recommended Action
1. Keep portal/database-schema.sql as the main schema
2. Keep utility scripts in portal/sql/ folder
3. Keep seed.sql for reference/testing
4. Remove all numbered migration scripts from database-scripts/
5. Remove redundant schema files from database/ and migrations/ folders
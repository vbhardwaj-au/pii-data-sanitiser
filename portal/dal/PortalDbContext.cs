using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<DatabaseSchema> DatabaseSchemas { get; set; }
    public DbSet<TableColumn> TableColumns { get; set; }
    public DbSet<ColumnObfuscationMapping> ColumnObfuscationMappings { get; set; }
    public DbSet<ObfuscationConfiguration> ObfuscationConfigurations { get; set; }
    public DbSet<ProductConnectionString> ProductConnectionStrings { get; set; }
    public DbSet<PipelineStage> PipelineStages { get; set; }
    public DbSet<ProductPipelineConfiguration> ProductPipelineConfigurations { get; set; }
    public DbSet<PipelineStageScript> PipelineStageScripts { get; set; }
    public DbSet<ScriptExecutionLog> ScriptExecutionLogs { get; set; }
    public DbSet<PipelineExecution> PipelineExecutions { get; set; }
    public DbSet<StageExecution> StageExecutions { get; set; }
    public DbSet<ObfuscationAgent> ObfuscationAgents { get; set; }
    public DbSet<ObfuscationJob> ObfuscationJobs { get; set; }
    public DbSet<JobExecutionLog> JobExecutionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DatabaseTechnology).IsRequired().HasMaxLength(50);
            entity.Property(e => e.GlobalSeed).HasMaxLength(255);
            entity.Property(e => e.MappingCacheDirectory).HasMaxLength(500);
            entity.Property(e => e.ConnectionString).IsRequired();
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);

            // Inform EF Core that this table has a trigger so it avoids using a plain OUTPUT clause
            entity.ToTable(tb => tb.HasTrigger("tr_Products_Audit"));
        });

        // DatabaseSchema configuration
        modelBuilder.Entity<DatabaseSchema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SchemaName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TableName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FullTableName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PrimaryKeyColumns);
            entity.Property(e => e.RowCount).HasColumnType("bigint");
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.DatabaseSchemas)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.FullTableName);
            entity.HasIndex(e => e.IsAnalyzed);
        });

        // TableColumn configuration
        modelBuilder.Entity<TableColumn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ColumnName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.SqlDataType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DefaultValue);
            entity.Property(e => e.CharacterSet).HasMaxLength(100);
            entity.Property(e => e.Collation).HasMaxLength(100);
            
            entity.HasOne(d => d.DatabaseSchema)
                .WithMany(p => p.TableColumns)
                .HasForeignKey(d => d.DatabaseSchemaId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.DatabaseSchemaId);
            entity.HasIndex(e => e.ColumnName);
            entity.HasIndex(e => e.IsPrimaryKey);
        });

        // ColumnObfuscationMapping configuration
        modelBuilder.Entity<ColumnObfuscationMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ObfuscationDataType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DetectionReasons);
            entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(3,2)");
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ColumnObfuscationMappings)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(d => d.TableColumn)
                .WithOne(p => p.ColumnObfuscationMapping)
                .HasForeignKey<ColumnObfuscationMapping>(d => d.TableColumnId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.TableColumnId);
            entity.HasIndex(e => e.IsEnabled);
        });

        // ObfuscationConfiguration configuration
        modelBuilder.Entity<ObfuscationConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ConfigurationJson).IsRequired();
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ObfuscationConfigurations)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsDefault);
        });

        // ProductConnectionString configuration
        modelBuilder.Entity<ProductConnectionString>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ConnectionString).IsRequired().HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
            entity.Property(e => e.UpdatedBy).HasMaxLength(255);
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ProductConnectionStrings)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Unique constraint: One connection type per product
            entity.HasIndex(e => new { e.ProductId, e.ConnectionType })
                .IsUnique()
                .HasDatabaseName("UQ_ProductConnectionStrings_Type");
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.ConnectionType);
            entity.HasIndex(e => e.IsActive);
        });

        // PipelineStage configuration
        modelBuilder.Entity<PipelineStage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ConfigurationTemplate).HasColumnType("NVARCHAR(MAX)");
            
            // Unique constraint on name
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.ExecutionOrder);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsRequired);
        });

        // ProductPipelineConfiguration configuration
        modelBuilder.Entity<ProductPipelineConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Configuration).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.OnFailureAction).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
            entity.Property(e => e.UpdatedBy).HasMaxLength(255);
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ProductPipelineConfigurations)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(d => d.PipelineStage)
                .WithMany(ps => ps.ProductPipelineConfigurations)
                .HasForeignKey(d => d.PipelineStageId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Unique constraint: One configuration per product per stage
            entity.HasIndex(e => new { e.ProductId, e.PipelineStageId })
                .IsUnique()
                .HasDatabaseName("UQ_ProductPipelineConfiguration_ProductStage");
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.PipelineStageId);
            entity.HasIndex(e => e.IsEnabled);
        });

        // PipelineStageScript configuration
        modelBuilder.Entity<PipelineStageScript>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.SqlScript).IsRequired().HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.OnFailureAction).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ExpectedResultType).HasMaxLength(50);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.SecurityValidationNotes).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
            entity.Property(e => e.UpdatedBy).HasMaxLength(255);
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.PipelineStageScripts)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(d => d.PipelineStage)
                .WithMany(ps => ps.PipelineStageScripts)
                .HasForeignKey(d => d.PipelineStageId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.PipelineStageId);
            entity.HasIndex(e => new { e.ProductId, e.PipelineStageId, e.ExecutionOrder });
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.IsSecurityValidated);
        });

        // ScriptExecutionLog configuration
        modelBuilder.Entity<ScriptExecutionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ErrorStackTrace).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ExecutedSql).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ConnectionType).HasMaxLength(50);
            entity.Property(e => e.ExecutionTarget).HasMaxLength(255);
            entity.Property(e => e.ExecutionMetadata).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ExecutedBy).HasMaxLength(255);
            
            entity.HasOne(d => d.Script)
                .WithMany(s => s.ExecutionLogs)
                .HasForeignKey(d => d.ScriptId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ScriptId);
            entity.HasIndex(e => e.PipelineExecutionId);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);
        });

        // PipelineExecution configuration
        modelBuilder.Entity<PipelineExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ErrorStackTrace).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.InitiatedBy).HasMaxLength(255);
            entity.Property(e => e.CancellationReason).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ConfigurationSnapshot).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ExecutionParameters).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ConnectionType).HasMaxLength(50);
            entity.Property(e => e.Tags).HasMaxLength(500);
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.PipelineExecutions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QueuedAt);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.IsDryRun);
        });

        // StageExecution configuration
        modelBuilder.Entity<StageExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StageName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.OnFailureAction).HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ErrorStackTrace).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ConfigurationSnapshot).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ExecutionMetadata).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.OutputSummary).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Warnings).HasColumnType("NVARCHAR(MAX)");
            
            entity.HasOne(d => d.PipelineExecution)
                .WithMany(pe => pe.StageExecutions)
                .HasForeignKey(d => d.PipelineExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(d => d.PipelineStage)
                .WithMany()
                .HasForeignKey(d => d.PipelineStageId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => e.PipelineExecutionId);
            entity.HasIndex(e => e.PipelineStageId);
            entity.HasIndex(e => e.ExecutionOrder);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QueuedAt);
            entity.HasIndex(e => e.StartedAt);
        });

        // ObfuscationAgent configuration
        modelBuilder.Entity<ObfuscationAgent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HostName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Environment).HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Capabilities)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<AgentCapabilities>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new AgentCapabilities()
                )
                .HasColumnType("NVARCHAR(MAX)");

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.LastHeartbeat);
            entity.HasIndex(e => new { e.HostName, e.IpAddress });
        });

        // ObfuscationJob configuration
        modelBuilder.Entity<ObfuscationJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.MappingVersion).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MappingSasUrl).IsRequired().HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ResultSummary).HasMaxLength(4000);
            entity.Property(e => e.ResultsBlobUrl).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.Configuration)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<JobConfiguration>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new JobConfiguration()
                )
                .HasColumnType("NVARCHAR(MAX)");

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Agent)
                .WithMany(a => a.Jobs)
                .HasForeignKey(d => d.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.StartedAt);
        });

        // JobExecutionLog configuration
        modelBuilder.Entity<JobExecutionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Component).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).IsRequired().HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Details).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Exception).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.TableName).HasMaxLength(100);
            entity.Property(e => e.ColumnName).HasMaxLength(100);

            entity.HasOne(d => d.Job)
                .WithMany(j => j.ExecutionLogs)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.LogLevel);
        });
    }
}

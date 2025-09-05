using DAL;
using Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Services
{
    /// <summary>
    /// Service for seeding default pipeline stages
    /// </summary>
    public class PipelineStageSeederService : IPipelineStageSeederService
    {
        private readonly PortalDbContext _context;
        private readonly ILogger<PipelineStageSeederService> _logger;

        public PipelineStageSeederService(PortalDbContext context, ILogger<PipelineStageSeederService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seeds the default pipeline stages if they don't exist
        /// </summary>
        public async Task SeedDefaultStagesAsync()
        {
            if (await AreDefaultStagesSeededAsync())
            {
                _logger.LogInformation("Default pipeline stages already exist, skipping seeding");
                return;
            }

            _logger.LogInformation("Seeding default pipeline stages...");

            var defaultStages = new[]
            {
                new PipelineStage
                {
                    Id = Guid.NewGuid(),
                    Name = PipelineStageNames.DataReduction,
                    DisplayName = "Data Reduction",
                    Description = "Reduce dataset size by filtering unnecessary data before obfuscation",
                    ExecutionOrder = 1,
                    IsRequired = false,
                    IsActive = true,
                    DefaultTimeoutMinutes = 30,
                    SupportsParallelExecution = true,
                    ConfigurationTemplate = @"{
  ""enabled"": true,
  ""scripts"": [],
  ""timeoutMinutes"": 30,
  ""parallelExecution"": true,
  ""onFailure"": ""stop""
}"
                },
                new PipelineStage
                {
                    Id = Guid.NewGuid(),
                    Name = PipelineStageNames.PreObfuscation,
                    DisplayName = "Pre-Obfuscation",
                    Description = "Execute custom SQL scripts before data obfuscation begins",
                    ExecutionOrder = 2,
                    IsRequired = false,
                    IsActive = true,
                    DefaultTimeoutMinutes = 45,
                    SupportsParallelExecution = false,
                    ConfigurationTemplate = @"{
  ""enabled"": true,
  ""scripts"": [],
  ""timeoutMinutes"": 45,
  ""parallelExecution"": false,
  ""onFailure"": ""stop""
}"
                },
                new PipelineStage
                {
                    Id = Guid.NewGuid(),
                    Name = PipelineStageNames.Obfuscation,
                    DisplayName = "Data Obfuscation",
                    Description = "Core obfuscation process using the existing PII obfuscation engine",
                    ExecutionOrder = 3,
                    IsRequired = true,
                    IsActive = true,
                    DefaultTimeoutMinutes = 120,
                    SupportsParallelExecution = true,
                    ConfigurationTemplate = @"{
  ""enabled"": true,
  ""timeoutMinutes"": 120,
  ""parallelExecution"": true,
  ""batchSize"": 1000,
  ""threads"": 8,
  ""onFailure"": ""stop""
}"
                },
                new PipelineStage
                {
                    Id = Guid.NewGuid(),
                    Name = PipelineStageNames.PostObfuscation,
                    DisplayName = "Post-Obfuscation",
                    Description = "Execute custom SQL scripts after data obfuscation is complete",
                    ExecutionOrder = 4,
                    IsRequired = false,
                    IsActive = true,
                    DefaultTimeoutMinutes = 45,
                    SupportsParallelExecution = false,
                    ConfigurationTemplate = @"{
  ""enabled"": true,
  ""scripts"": [],
  ""timeoutMinutes"": 45,
  ""parallelExecution"": false,
  ""onFailure"": ""stop""
}"
                },
                new PipelineStage
                {
                    Id = Guid.NewGuid(),
                    Name = PipelineStageNames.Testing,
                    DisplayName = "Testing & Validation",
                    Description = "Run data quality tests and validation scripts to verify obfuscation results",
                    ExecutionOrder = 5,
                    IsRequired = false,
                    IsActive = true,
                    DefaultTimeoutMinutes = 30,
                    SupportsParallelExecution = true,
                    ConfigurationTemplate = @"{
  ""enabled"": true,
  ""scripts"": [],
  ""timeoutMinutes"": 30,
  ""parallelExecution"": true,
  ""onFailure"": ""warn""
}"
                }
            };

            await _context.PipelineStages.AddRangeAsync(defaultStages);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} default pipeline stages", defaultStages.Length);
        }

        /// <summary>
        /// Checks if default stages have been seeded
        /// </summary>
        public async Task<bool> AreDefaultStagesSeededAsync()
        {
            return await _context.PipelineStages.AnyAsync();
        }
    }
}
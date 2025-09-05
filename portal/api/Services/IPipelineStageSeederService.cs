namespace API.Services
{
    /// <summary>
    /// Service interface for seeding default pipeline stages
    /// </summary>
    public interface IPipelineStageSeederService
    {
        /// <summary>
        /// Seeds the default pipeline stages if they don't exist
        /// </summary>
        Task SeedDefaultStagesAsync();

        /// <summary>
        /// Checks if default stages have been seeded
        /// </summary>
        Task<bool> AreDefaultStagesSeededAsync();
    }
}
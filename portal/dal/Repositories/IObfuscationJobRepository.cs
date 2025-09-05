using Contracts.Models;

namespace DAL.Repositories;

public interface IObfuscationJobRepository
{
    Task<ObfuscationJob?> GetByIdAsync(Guid id);
    Task<IEnumerable<ObfuscationJob>> GetAllAsync();
    Task<IEnumerable<ObfuscationJob>> GetByProductIdAsync(Guid productId);
    Task<IEnumerable<ObfuscationJob>> GetByAgentIdAsync(Guid agentId);
    Task<IEnumerable<ObfuscationJob>> GetByStatusAsync(JobStatus status);
    Task<IEnumerable<ObfuscationJob>> GetPendingJobsAsync();
    Task<IEnumerable<ObfuscationJob>> GetActiveJobsAsync();
    Task<IEnumerable<ObfuscationJob>> GetCompletedJobsAsync(int days = 7);
    Task<IEnumerable<ObfuscationJob>> GetFailedJobsAsync(int days = 7);
    Task<ObfuscationJob> CreateAsync(ObfuscationJob job);
    Task<ObfuscationJob> UpdateAsync(ObfuscationJob job);
    Task<bool> DeleteAsync(Guid id);
    Task UpdateStatusAsync(Guid jobId, JobStatus status);
    Task UpdateProgressAsync(Guid jobId, decimal progressPercentage, long processedRows, int tablesCompleted);
    Task AssignJobToAgentAsync(Guid jobId, Guid agentId);
    Task<ObfuscationJob?> GetNextJobForAgentAsync(Guid agentId);
    Task<IEnumerable<ObfuscationJob>> GetJobsReadyForAssignmentAsync(int maxJobs = 10);
    Task MarkJobAsStartedAsync(Guid jobId, DateTime startTime);
    Task MarkJobAsCompletedAsync(Guid jobId, DateTime completionTime, string? resultSummary = null, string? resultsBlobUrl = null);
    Task MarkJobAsFailedAsync(Guid jobId, string errorMessage, bool shouldRetry = true);
    Task<int> GetQueueLengthAsync();
    Task<IEnumerable<ObfuscationJob>> GetJobsByPriorityAsync(JobPriority priority);
}
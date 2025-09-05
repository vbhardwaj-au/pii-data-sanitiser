using Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories;

public class ObfuscationJobRepository : IObfuscationJobRepository
{
    private readonly PortalDbContext _context;

    public ObfuscationJobRepository(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<ObfuscationJob?> GetByIdAsync(Guid id)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .Include(j => j.ExecutionLogs.OrderByDescending(l => l.Timestamp).Take(50))
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<IEnumerable<ObfuscationJob>> GetAllAsync()
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetByProductIdAsync(Guid productId)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Agent)
            .Where(j => j.ProductId == productId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetByAgentIdAsync(Guid agentId)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Where(j => j.AgentId == agentId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetByStatusAsync(JobStatus status)
    {
        // Removed Include statements to avoid null reference issues
        // The Product and Agent navigation properties will be lazy-loaded if needed
        return await _context.ObfuscationJobs
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetPendingJobsAsync()
    {
        try
        {
            // Try with Include first
            return await _context.ObfuscationJobs
                .Include(j => j.Product)
                .Where(j => j.Status == JobStatus.Pending && j.AgentId == null)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .ToListAsync();
        }
        catch
        {
            // If Include fails, get without it
            return await _context.ObfuscationJobs
                .Where(j => j.Status == JobStatus.Pending && j.AgentId == null)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .ToListAsync();
        }
    }

    public async Task<IEnumerable<ObfuscationJob>> GetActiveJobsAsync()
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Assigned)
            .OrderByDescending(j => j.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetCompletedJobsAsync(int days = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .Where(j => j.Status == JobStatus.Completed && j.CompletedAt >= cutoffDate)
            .OrderByDescending(j => j.CompletedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetFailedJobsAsync(int days = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .Where(j => j.Status == JobStatus.Failed && j.LastUpdateAt >= cutoffDate)
            .OrderByDescending(j => j.LastUpdateAt)
            .ToListAsync();
    }

    public async Task<ObfuscationJob> CreateAsync(ObfuscationJob job)
    {
        job.Id = Guid.NewGuid();
        job.CreatedAt = DateTime.UtcNow;
        job.LastUpdateAt = DateTime.UtcNow;
        
        _context.ObfuscationJobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<ObfuscationJob> UpdateAsync(ObfuscationJob job)
    {
        job.LastUpdateAt = DateTime.UtcNow;
        _context.ObfuscationJobs.Update(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var job = await _context.ObfuscationJobs.FindAsync(id);
        if (job == null)
            return false;

        _context.ObfuscationJobs.Remove(job);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UpdateStatusAsync(Guid jobId, JobStatus status)
    {
        await _context.ObfuscationJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));
    }

    public async Task UpdateProgressAsync(Guid jobId, decimal progressPercentage, long processedRows, int tablesCompleted)
    {
        await _context.ObfuscationJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.ProgressPercentage, progressPercentage)
                .SetProperty(j => j.ProcessedRows, processedRows)
                .SetProperty(j => j.TablesCompleted, tablesCompleted)
                .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));
    }

    public async Task AssignJobToAgentAsync(Guid jobId, Guid agentId)
    {
        await _context.ObfuscationJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.AgentId, agentId)
                .SetProperty(j => j.Status, JobStatus.Assigned)
                .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));

        // Update agent active job count
        await _context.ObfuscationAgents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.ActiveJobs, a => a.ActiveJobs + 1));
    }

    public async Task<ObfuscationJob?> GetNextJobForAgentAsync(Guid agentId)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Where(j => j.AgentId == agentId && j.Status == JobStatus.Assigned)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ObfuscationJob>> GetJobsReadyForAssignmentAsync(int maxJobs = 10)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Where(j => j.Status == JobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Take(maxJobs)
            .ToListAsync();
    }

    public async Task MarkJobAsStartedAsync(Guid jobId, DateTime startTime)
    {
        await _context.ObfuscationJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Running)
                .SetProperty(j => j.StartedAt, startTime)
                .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));
    }

    public async Task MarkJobAsCompletedAsync(Guid jobId, DateTime completionTime, string? resultSummary = null, string? resultsBlobUrl = null)
    {
        var job = await _context.ObfuscationJobs.FindAsync(jobId);
        if (job != null)
        {
            await _context.ObfuscationJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Completed)
                    .SetProperty(j => j.CompletedAt, completionTime)
                    .SetProperty(j => j.ResultSummary, resultSummary ?? string.Empty)
                    .SetProperty(j => j.ResultsBlobUrl, resultsBlobUrl)
                    .SetProperty(j => j.Duration, completionTime - (job.StartedAt ?? completionTime))
                    .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));

            // Update agent metrics
            await _context.ObfuscationAgents
                .Where(a => a.Id == job.AgentId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.ActiveJobs, a => a.ActiveJobs > 0 ? a.ActiveJobs - 1 : 0)
                    .SetProperty(a => a.CompletedJobs, a => a.CompletedJobs + 1)
                    .SetProperty(a => a.LastJobCompleted, completionTime));
        }
    }

    public async Task MarkJobAsFailedAsync(Guid jobId, string errorMessage, bool shouldRetry = true)
    {
        var job = await _context.ObfuscationJobs.FindAsync(jobId);
        if (job != null)
        {
            var newStatus = (shouldRetry && job.RetryCount < job.MaxRetries) ? JobStatus.Retrying : JobStatus.Failed;
            var newRetryCount = shouldRetry ? job.RetryCount + 1 : job.RetryCount;

            await _context.ObfuscationJobs
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, newStatus)
                    .SetProperty(j => j.ErrorMessage, errorMessage)
                    .SetProperty(j => j.RetryCount, newRetryCount)
                    .SetProperty(j => j.LastUpdateAt, DateTime.UtcNow));

            // Update agent metrics
            await _context.ObfuscationAgents
                .Where(a => a.Id == job.AgentId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.ActiveJobs, a => a.ActiveJobs > 0 ? a.ActiveJobs - 1 : 0)
                    .SetProperty(a => a.FailedJobs, a => a.FailedJobs + 1));
        }
    }

    public async Task<int> GetQueueLengthAsync()
    {
        return await _context.ObfuscationJobs
            .CountAsync(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned);
    }

    public async Task<IEnumerable<ObfuscationJob>> GetJobsByPriorityAsync(JobPriority priority)
    {
        return await _context.ObfuscationJobs
            .Include(j => j.Product)
            .Include(j => j.Agent)
            .Where(j => j.Priority == priority)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }
}
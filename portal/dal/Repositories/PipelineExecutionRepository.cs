using Microsoft.EntityFrameworkCore;
using Contracts.Models;
using DAL;

namespace DAL.Repositories;

public class PipelineExecutionRepository : IPipelineExecutionRepository
{
    private readonly PortalDbContext _context;

    public PipelineExecutionRepository(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PipelineExecution>> GetAllAsync()
    {
        return await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
            .OrderByDescending(pe => pe.QueuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<PipelineExecution>> GetByProductIdAsync(Guid productId)
    {
        return await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
            .Where(pe => pe.ProductId == productId)
            .OrderByDescending(pe => pe.QueuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<PipelineExecution>> GetActiveExecutionsAsync()
    {
        return await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
            .Where(pe => pe.Status == PipelineExecutionStatus.Running || pe.Status == PipelineExecutionStatus.Queued)
            .OrderByDescending(pe => pe.QueuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<PipelineExecution>> GetRecentExecutionsAsync(int count = 50)
    {
        return await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
            .OrderByDescending(pe => pe.QueuedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<PipelineExecution?> GetByIdAsync(Guid id)
    {
        return await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .FirstOrDefaultAsync(pe => pe.Id == id);
    }

    // Removed GetByHangfireJobIdAsync - no longer using Hangfire

    public async Task<PipelineExecution> CreateAsync(PipelineExecution execution)
    {
        execution.Id = Guid.NewGuid();
        execution.QueuedAt = DateTime.UtcNow;
        
        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync();
        
        return execution;
    }

    public async Task<PipelineExecution> UpdateAsync(PipelineExecution execution)
    {
        _context.PipelineExecutions.Update(execution);
        await _context.SaveChangesAsync();
        
        return execution;
    }

    public async Task DeleteAsync(Guid id)
    {
        var execution = await _context.PipelineExecutions.FindAsync(id);
        if (execution != null)
        {
            _context.PipelineExecutions.Remove(execution);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.PipelineExecutions.AnyAsync(pe => pe.Id == id);
    }

    public async Task<int> GetCountByStatusAsync(string status)
    {
        return await _context.PipelineExecutions
            .CountAsync(pe => pe.Status == status);
    }

    public async Task<int> GetTodayCountAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.PipelineExecutions
            .CountAsync(pe => pe.QueuedAt >= today);
    }

    public async Task<int> GetTodaySuccessfulCountAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.PipelineExecutions
            .CountAsync(pe => pe.QueuedAt >= today && pe.Status == PipelineExecutionStatus.Completed);
    }

    public async Task<int> GetTodayFailedCountAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.PipelineExecutions
            .CountAsync(pe => pe.QueuedAt >= today && pe.Status == PipelineExecutionStatus.Failed);
    }

    public async Task<IEnumerable<PipelineExecution>> GetExecutionHistoryAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? productName = null,
        int limit = 100)
    {
        var query = _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(pe => pe.Status == status);
        }

        if (startDate.HasValue)
        {
            query = query.Where(pe => pe.QueuedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(pe => pe.QueuedAt <= endOfDay);
        }

        if (!string.IsNullOrEmpty(productName))
        {
            query = query.Where(pe => pe.Product != null && pe.Product.Name.Contains(productName));
        }

        return await query
            .OrderByDescending(pe => pe.QueuedAt)
            .Take(limit)
            .ToListAsync();
    }
}
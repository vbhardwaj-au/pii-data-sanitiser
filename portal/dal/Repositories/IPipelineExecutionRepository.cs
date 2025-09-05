using Contracts.Models;
using Contracts.DTOs;

namespace DAL.Repositories;

public interface IPipelineExecutionRepository
{
    Task<IEnumerable<PipelineExecution>> GetAllAsync();
    Task<IEnumerable<PipelineExecution>> GetByProductIdAsync(Guid productId);
    Task<IEnumerable<PipelineExecution>> GetActiveExecutionsAsync();
    Task<IEnumerable<PipelineExecution>> GetRecentExecutionsAsync(int count = 50);
    Task<PipelineExecution?> GetByIdAsync(Guid id);
    Task<PipelineExecution> CreateAsync(PipelineExecution execution);
    Task<PipelineExecution> UpdateAsync(PipelineExecution execution);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<int> GetCountByStatusAsync(string status);
    Task<int> GetTodayCountAsync();
    Task<int> GetTodaySuccessfulCountAsync();
    Task<int> GetTodayFailedCountAsync();
    Task<IEnumerable<PipelineExecution>> GetExecutionHistoryAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? productName = null,
        int limit = 100);
}
using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IStrategyRepository : IRepository<Strategy>
{
    Task<List<Strategy>> GetActiveStrategyAsync(CancellationToken cancellationToken = default);
    Task<List<Strategy>> GetAllAsync();
}

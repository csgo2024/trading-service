using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IStrategyRepository : IRepository<Strategy>
{
    Task<Strategy?> Add(Strategy entity, CancellationToken cancellationToken = default);
    Task<List<Strategy>> GetActiveStrategyAsync(CancellationToken cancellationToken = default);
    Task<List<Strategy>> GetAllStrategies();
    Task<List<Strategy>> GetActiveStrategyByTypeAsync(StrategyType strategyType,
                                                      CancellationToken cancellationToken = default);
}

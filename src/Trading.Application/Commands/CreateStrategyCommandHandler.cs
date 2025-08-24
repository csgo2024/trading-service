using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateStrategyCommandHandler : IRequestHandler<CreateStrategyCommand, Strategy>
{
    private readonly IStrategyRepository _strategyRepository;
    public CreateStrategyCommandHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<Strategy> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate the command
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errorMessage = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ValidationException(errorMessage);
        }
        var entity = new Strategy(
            request.Symbol.ToUpper(),
            request.Amount,
            request.Volatility,
            request.Leverage,
            request.AccountType,
            request.Interval,
            request.StrategyType,
            request.AutoReset,
            request.StopLossExpression
        );
        entity = await _strategyRepository.AddAsync(entity, cancellationToken);
        return entity;
    }
}

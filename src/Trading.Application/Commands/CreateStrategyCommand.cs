using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Common.Attributes;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Exchange.Binance.Attributes;

namespace Trading.Application.Commands;

public class CreateStrategyCommand : IRequest<Strategy>, IValidatableObject
{
    [Required(ErrorMessage = "Symbol cannot be empty")]
    public string Symbol { get; set; } = string.Empty;

    [Range(10, int.MaxValue, ErrorMessage = "Amount must be greater than 10")]
    public int Amount { get; set; }

    [Range(0.00001, 0.9, ErrorMessage = "Volatility must be between 0.00001 and 0.9")]
    public decimal Volatility { get; set; }

    [Range(1, 20, ErrorMessage = "Leverage must be between 1 and 20")]
    public int? Leverage { get; set; }

    public AccountType AccountType { get; set; } = AccountType.Spot;

    public StrategyType StrategyType { get; set; } = StrategyType.OpenBuy;

    [Interval]
    public string? Interval { get; set; }

    [JavaScript(Required = false)]
    public string StopLossExpression { get; set; } = string.Empty;

    public bool AutoReset { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AccountType == AccountType.Spot)
        {
            if (StrategyType == StrategyType.OpenSell || StrategyType == StrategyType.CloseSell)
            {
                yield return new ValidationResult("Spot account type is not supported for OpenSell or CloseSell strategy.", [nameof(AccountType)]);
            }
        }
    }
}

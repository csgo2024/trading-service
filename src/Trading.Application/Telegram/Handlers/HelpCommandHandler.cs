using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Telegram.Handlers;

public class HelpCommandHandler : ICommandHandler
{
    private readonly ILogger<HelpCommandHandler> _logger;
    public static string Command => "/help";
    public const string HelpText = """
*基础命令:*
/help \- 显示此帮助信息
/debug \- 显示运行状态
/strategy \- [create\|delete\|pause\|resume] 策略管理
/alert \- [create\|delete\|empty\|pause\|resume] 警报相关

*策略类型说明*
  \- *OpenBuy* 和 *OpenSell*: 基于指定周期开盘价格执行的策略
  \- 特点：不需要等待收盘，自动获取当前周期的开盘价格进行下单，如果AutoReset为true，则会在每个新周期开始时取消未成交订单并重新下单
  \- *CloseBuy* 和 *CloseSell*: 基于指定周期收盘价格执行的策略
  \- ⚠️ 注意：必须等待当前周期收盘后才会执行下单，不会在新周期开始时自动更新价格下单

*策略示例*

1\. 现货做多策略 \(OpenBuy\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.2,
  "Interval": "1d",
  "AccountType": "Spot",
  "AutoReset": true
}`

2\. 合约做空策略 \(OpenSell\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.2,
  "Interval": "1d",
  "AccountType": "Future",
  "StrategyType": "OpenSell",
  "AutoReset": true
}`

3\. WebSocket合约做空策略 \(CloseSell\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.002,
  "Interval": "4h",
  "AccountType": "Future",
  "StrategyType": "CloseSell",
  "StopLossExpression": "close >= 1000000"
}`

4\. WebSocket合约做多策略 \(CloseBuy\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.002,
  "Interval": "4h",
  "AccountType": "Future",
  "StrategyType": "CloseBuy",
  "StopLossExpression": "close <= 1"
}`

删除策略:
`/strategy delete <Id>`

*警报管理*
创建警报\(支持间隔: 5m,15m,1h,4h,1d,3d,1w\):

1\. 价格波动警报
`/alert create 
{
  "Symbol":"BTCUSDT",
  "Interval":"4h",
  "Expression":"Math.abs((close-open)/open)>=0.02"
}`

2\. 价格阈值警报
`/alert create 
{
  "Symbol":"BTCUSDT",
  "Interval":"4h",
  "Expression":"close>=20000"
}`

删除警报:
`/alert delete <Id>`

清空警报:
`/alert empty`
""";

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(string parameters)
    {
        var telegramScope = new TelegramLoggerScope
        {
            ParseMode = ParseMode.MarkdownV2,
        };

        using (_logger.BeginScope(telegramScope))
        {
#pragma warning disable CA2017 // Parameter count mismatch
            await Task.Run(() => _logger.Log(LogLevel.Information, Logging.LoggerExtensions.NotificationEventId, HelpText));
#pragma warning restore CA2017 // Parameter count mismatch
        }
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}

using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Account;

public interface IAccountProcessor
{
    Task<WebCallResult<BinanceOrderBase>> GetOrder(string symbol,
                                                   long? orderId,
                                                   CancellationToken ct);
    /// <summary>
    /// If startTime and endTime are not sent, the most recent klines are returned.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="interval"></param>
    /// <param name="startTime">https://developers.binance.com/docs/derivatives/usds-margined-futures/market-data/rest-api/Kline-Candlestick-Data</param>
    /// <param name="endTime"></param>
    /// <param name="limit"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<WebCallResult<IBinanceKline[]>> GetKlines(string symbol,
                                                              KlineInterval interval,
                                                              DateTime? startTime = null,
                                                              DateTime? endTime = null,
                                                              int? limit = null,
                                                              CancellationToken ct = default);
    Task<WebCallResult<BinanceOrderBase>> PlaceLongOrderAsync(string symbol,
                                                              decimal quantity,
                                                              decimal price,
                                                              TimeInForce timeInForce,
                                                              CancellationToken ct);
    Task<WebCallResult<BinanceOrderBase>> PlaceShortOrderAsync(string symbol,
                                                               decimal quantity,
                                                               decimal price,
                                                               TimeInForce timeInForce,
                                                               CancellationToken ct);
    Task<WebCallResult<BinanceOrderBase>> CancelOrderAsync(string symbol,
                                                      long orderId,
                                                      CancellationToken ct);
    Task<(BinanceSymbolPriceFilter?, BinanceSymbolLotSizeFilter?)> GetSymbolFilterData(Strategy strategy,
                                                                                       CancellationToken ct = default);
    Task<WebCallResult<BinanceOrderBase>> StopLongOrderAsync(string symbol,
                                                              decimal quantity,
                                                              decimal price,
                                                              CancellationToken ct);
    Task<WebCallResult<BinanceOrderBase>> StopShortOrderAsync(string symbol,
                                                              decimal quantity,
                                                              decimal price,
                                                              CancellationToken ct);
}

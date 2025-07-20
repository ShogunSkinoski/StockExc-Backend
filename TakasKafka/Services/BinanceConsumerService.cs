using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TakasKafka.Data;
using TakasKafka.DataHub;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class BinanceConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<BinanceConsumerService> _logger;
    private readonly IServiceScopeFactory _factory;

    public BinanceConsumerService(IConfigurationRoot _configuration, ILogger<BinanceConsumerService> logger, IServiceScopeFactory factory)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration.GetConnectionString("kafka-container"),
            GroupId = "binance-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false
        };
       
       _logger = logger;
       _factory = factory;
       _consumer = new ConsumerBuilder<string, string>(config)
               .SetErrorHandler((_, e) =>
               {
                   _logger.LogError($"Kafka consumer error: {e.Reason}");
               })
               .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("binance-trades");
        _logger.LogInformation("Binance trade consumer service started");

        await Task.Factory.StartNew(async () =>
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResults = _consumer.ConsumeBatch(TimeSpan.FromMilliseconds(1000), 500);
                        
                        if (consumeResults.Any())
                        {
                            await ProcessBinanceTradeBatch(consumeResults, stoppingToken);
                            
                            foreach (var result in consumeResults)
                            {
                                _consumer.StoreOffset(result);
                            }
                            _consumer.Commit();
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, $"Error consuming Binance trade message: {ex.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing Binance trade batch: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatal error in Binance consumer: {ex.Message}");
            }
            finally
            {
                _consumer.Close();
            }
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private async Task ProcessBinanceTradeBatch(IEnumerable<ConsumeResult<string, string>> consumeResults, CancellationToken stoppingToken)
    {
        using var scope = _factory.CreateScope();
        var stockExchangeDbContext = scope.ServiceProvider.GetRequiredService<StockExchangeDbContext>();
        var marketDataService = scope.ServiceProvider.GetRequiredService<MarketDataService>();
        
        var binanceTrades = new List<BinanceTrade>();
        var priceUpdates = new Dictionary<string, (decimal price, string quantity)>();

        foreach (var consumeResult in consumeResults)
        {
            if (consumeResult?.Message?.Value != null)
            {
                try
                {
                    var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(consumeResult.Message.Value);
                    if (jsonDict != null)
                    {
                        var binanceTrade = BinanceTrade.MapFromDescriptiveDictionary(jsonDict);
                        binanceTrades.Add(binanceTrade);
                        
                        if (decimal.TryParse(binanceTrade.Price, out var priceDecimal))
                        {
                            priceUpdates[binanceTrade.TradingSymbol] = (priceDecimal/100000000, binanceTrade.Quantity);
                        }
                    }
                }
                catch (JsonException)
                {
                    try
                    {
                        var binanceTrade = JsonSerializer.Deserialize<BinanceTrade>(consumeResult.Message.Value);
                        if (binanceTrade != null)
                        {
                            binanceTrades.Add(binanceTrade);
                            
                            if (decimal.TryParse(binanceTrade.Price, out var priceDecimal))
                            {
                                priceUpdates[binanceTrade.TradingSymbol] = (priceDecimal, binanceTrade.Quantity);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to deserialize Binance trade message: {consumeResult.Message.Value}");
                    }
                }
            }
        }
        
        if (binanceTrades.Count > 0)
        {
            await SaveBinanceTradesBatch(stockExchangeDbContext, binanceTrades);
            
            await UpdateMarketDataAndNotify(marketDataService, priceUpdates, scope);
            
            _logger.LogInformation($"Processed {binanceTrades.Count} Binance trades in batch");
        }
    }

    private async Task SaveBinanceTradesBatch(StockExchangeDbContext context, List<BinanceTrade> binanceTrades)
    {
        try
        {
            var tradeIds = binanceTrades.Select(bt => bt.TradeId).ToList();
            var existingIds = await context.BinanceTrades
                .Where(bt => tradeIds.Contains(bt.TradeId))
                .Select(bt => bt.TradeId)
                .ToListAsync();
            
            var newTrades = binanceTrades.Where(bt => !existingIds.Contains(bt.TradeId)).ToList();
            
            if (newTrades.Count > 0)
            {
                await context.BinanceTrades.AddRangeAsync(newTrades);
                await context.SaveChangesAsync();
                _logger.LogInformation($"Saved {newTrades.Count} new Binance trades to database");
            }
            else
            {
                _logger.LogInformation("No new Binance trades to save - all were duplicates");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving Binance trades to database");
            throw;
        }
    }

    private async Task UpdateMarketDataAndNotify(MarketDataService marketDataService, 
        Dictionary<string, (decimal price, string quantity)> priceUpdates, 
        IServiceScope scope)
    {
        var marketHub = scope.ServiceProvider.GetRequiredService<IHubContext<MarketDataHub>>();
        
        foreach (var update in priceUpdates)
        {
            var symbol = update.Key;
            var (price, quantityStr) = update.Value;
            
            try
            {
                if (int.TryParse(quantityStr.Split('.')[0], out var volume))
                {
                    await marketDataService.UpdateMarketDataAsync(symbol, price, volume, MarketDataType.Trade);
                    
                    var marketUpdate = new
                    {
                        Symbol = symbol,
                        Price = price,
                        Volume = volume,
                        Source = "Binance",
                        Timestamp = DateTime.UtcNow
                    };
                    
                    await marketHub.Clients.Group($"market-{symbol}")
                        .SendAsync("BinancePriceUpdate", marketUpdate);
                    
                    await marketHub.Clients.Group($"market-{symbol}")
                        .SendAsync("PriceUpdate", new { Symbol = symbol, Price = price, Timestamp = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error updating market data for symbol {symbol}");
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

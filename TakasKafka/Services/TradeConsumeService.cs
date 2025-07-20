
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using TakasKafka.Data;
using TakasKafka.DataHub;
using TakasKafka.Messages;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class TradeConsumeService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<TradeConsumeService> _logger;
    private readonly IServiceScopeFactory _factory;

    public TradeConsumeService(IConfigurationRoot _configuration, ILogger<TradeConsumeService> logger, IServiceScopeFactory factory)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration.GetConnectionString("kafka-container"),
            GroupId = "stock-exchange-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
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
        _consumer.Subscribe("trades");
        _logger.LogInformation("Trade consumer service started");

        await Task.Factory.StartNew(async () =>
        {
            try
            {
                using var scope = _factory.CreateScope();
                var stockExchangeDbContext = scope.ServiceProvider.GetRequiredService<StockExchangeDbContext>();
                var marketDataService = scope.ServiceProvider.GetRequiredService<MarketDataService>();
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResults = _consumer.ConsumeBatch(TimeSpan.FromMilliseconds(500), 1000);
                        var messages = new List<TradeMessage>();
                        
                        foreach(var consumeResult in consumeResults)
                        {
                            if (consumeResult?.Message?.Value != null)
                            {
                                var tradeMessage = JsonSerializer.Deserialize<TradeMessage>(consumeResult.Message.Value);
                                if(tradeMessage != null)
                                {
                                    messages.Add(tradeMessage);
                                    
                                    await marketDataService.UpdateMarketDataAsync(
                                        tradeMessage.Symbol, 
                                        tradeMessage.Price, 
                                        tradeMessage.Quantity, 
                                        MarketDataType.Trade);
                                }
                            }
                        }
                        
                        if(messages.Count > 0)
                        {
                            await NotifyPriceServer(messages, marketDataService);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, $"Error consuming trade message: {ex.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing trade message: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatal error in trade consumer: {ex.Message}");
            }
            finally
            {
                _consumer.Close();
            }
        });
    }

    public async Task NotifyPriceServer(List<TradeMessage> tradeMessages, MarketDataService marketDataService)
    {
        using var scope = _factory.CreateScope();
        var marketHub = scope.ServiceProvider.GetRequiredService<IHubContext<MarketDataHub>>();
        
        var groupedMessages = tradeMessages.GroupBy(tm => tm.Symbol);
        
        foreach (var group in groupedMessages)
        {
            var symbol = group.Key;
            var trades = group.ToList();
            var currentPrice = await marketDataService.GetCurrentPriceAsync(symbol);
            
            var marketUpdate = new
            {
                Symbol = symbol,
                CurrentPrice = currentPrice,
                Trades = trades,
                LastUpdated = DateTime.UtcNow
            };
            
            await marketHub.Clients.Group($"market-{symbol}").SendAsync("PriceUpdate", new { Symbol = symbol, Price = currentPrice, Timestamp = DateTime.UtcNow });
            await marketHub.Clients.Group($"market-{symbol}").SendAsync("MarketUpdate", marketUpdate);
        }
    }
}

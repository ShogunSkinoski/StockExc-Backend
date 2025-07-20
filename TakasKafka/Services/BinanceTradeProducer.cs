
using Binance.Spot;
using System.Text.Json;
using TakasKafka.Data;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class BinanceTradeProducer : BackgroundService
{
    private readonly IServiceScopeFactory _factory;
    private readonly MarketDataWebSocket _dataWebSocket;
    private readonly ILogger<BinanceTradeProducer> _logger;
    private readonly StockExchangeDbContext _stockExchangeDbContext;
    private readonly KafkaProducerService _kafkaProducerService;
    private readonly string securitySymbol = "btcusdt";
    public BinanceTradeProducer(IServiceScopeFactory serviceScopeFactory, ILogger<BinanceTradeProducer> logger)
    {
        _factory = serviceScopeFactory;
        _dataWebSocket = new MarketDataWebSocket(securitySymbol + "@trade", "wss://stream.binance.com:9443/ws");
        _logger = logger;
        var scope = _factory.CreateScope();
        _stockExchangeDbContext = scope.ServiceProvider.GetRequiredService<StockExchangeDbContext>();
        _kafkaProducerService = scope.ServiceProvider.GetRequiredService<KafkaProducerService>();
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        { 

            _dataWebSocket.OnMessageReceived(async (data) =>
            {
                try
                {
                    int index = data.LastIndexOf('}');
                    var cleanedDataString = "";
                    if(index > 0)
                    {
                        cleanedDataString = data.Substring(0, index + 1);
                        var binanceTradeDict = JsonSerializer.Deserialize<Dictionary<string, object>>(cleanedDataString);
                        var binanceTrade = BinanceTrade.MapFromDictionary(binanceTradeDict);
                        if (binanceTrade != null)
                        {
                            
                            _stockExchangeDbContext.BinanceTrades.Add(binanceTrade);
                            _stockExchangeDbContext.SaveChanges();
                            await _kafkaProducerService.PublisBinanceTradeAsync(binanceTrade);
                        }
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex.Message);
                }

                await Task.CompletedTask;
            }, stoppingToken);

            await _dataWebSocket.ConnectAsync(stoppingToken);
        }
        catch { }
        
    }
}

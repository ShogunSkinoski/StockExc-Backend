using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using TakasKafka.Messages;
using TakasKafka.Models;
using TakasKafka.Services;

namespace TakasKafka.DataHub;

public class MarketDataHub : Hub
{
    private readonly MarketDataService _marketDataService;
    private readonly ILogger<MarketDataHub> _logger;

    public MarketDataHub(MarketDataService marketDataService, ILogger<MarketDataHub> logger)
    {
        _marketDataService = marketDataService;
        _logger = logger;
    }

    public async Task JoinMarketGroup(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"market-{symbol}");
        
        var currentPrice = await _marketDataService.GetCurrentPriceAsync(symbol);
        await Clients.Caller.SendAsync("CurrentPrice", new { Symbol = symbol, Price = currentPrice, Timestamp = DateTime.UtcNow });
        
        _logger.LogInformation($"Client {Context.ConnectionId} joined market group for {symbol}");
    }

    public async Task LeaveMarketGroup(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market-{symbol}");
        _logger.LogInformation($"Client {Context.ConnectionId} left market group for {symbol}");
    }

    public async Task GetPriceHistory(string symbol, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var history = await _marketDataService.GetPriceHistoryAsync(symbol, fromDate, toDate);
            await Clients.Caller.SendAsync("PriceHistory", new { Symbol = symbol, History = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting price history for {symbol}");
            await Clients.Caller.SendAsync("Error", $"Failed to get price history for {symbol}");
        }
    }

    public async Task RequestCurrentPrice(string symbol)
    {
        var currentPrice = await _marketDataService.GetCurrentPriceAsync(symbol);
        var latestData = await _marketDataService.GetLatestMarketDataAsync(symbol);
        
        await Clients.Caller.SendAsync("CurrentPrice", new 
        { 
            Symbol = symbol, 
            Price = currentPrice, 
            Timestamp = latestData?.Timestamp ?? DateTime.UtcNow 
        });
    }

    public async Task SendMessageToGroup(string symbol, TradeMessage trade)
    {
        var tradeJson = JsonSerializer.Serialize(trade);
        await Clients.Group($"market-{symbol}").SendAsync("TradeMessage", tradeJson);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client {Context.ConnectionId} connected to MarketDataHub");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} disconnected from MarketDataHub");
        await base.OnDisconnectedAsync(exception);
    }
}

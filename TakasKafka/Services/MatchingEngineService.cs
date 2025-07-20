using Microsoft.EntityFrameworkCore;
using TakasKafka.Data;
using TakasKafka.Messages;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class MatchingEngineService 
{
    private readonly StockExchangeDbContext _stockExchangeDbContext;
    private readonly KafkaProducerService _producerService;
    private readonly ILogger<MatchingEngineService> _logger;

    public MatchingEngineService(StockExchangeDbContext stockExchangeDbContext, KafkaProducerService producerService, ILogger<MatchingEngineService> logger)
    {
        _stockExchangeDbContext = stockExchangeDbContext;
        _producerService = producerService;
        _logger = logger;
    }

    public async Task ProcessNewOrderAsync(OrderMessage orderMessage)
    {
        try
        {
            var security = _stockExchangeDbContext.Securities.FirstOrDefault(security => security.Symbol == orderMessage.Symbol);

            if (security == null)
            {
                return;
            }

            var orderType = Enum.Parse<OrderType>(orderMessage.OrderType);
            
            decimal orderPrice = orderType == OrderType.Market ? 0 : orderMessage.Price;

            var order = new Order
            {
                ClientId = orderMessage.ClientId,
                SecurityId = security.Id,
                Type = orderType,
                Side = Enum.Parse<OrderSide>(orderMessage.Side),
                Quantity = orderMessage.Quantity,
                Price = orderPrice,
                Status = OrderStatus.Pending,
                CreatedAt = orderMessage.Timestamp,
                FilledQuantity = 0
            };

            _stockExchangeDbContext.Orders.Add(order);
            await _stockExchangeDbContext.SaveChangesAsync();

            await ProcessMatchingAsync(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing new order: {ex.Message}");
        }
    }

    public async Task ProcessMatchingAsync(Order newOrder)
    {
        var oppsiteOrders = await GetMatchingOrders(newOrder);

        foreach(var matchingOrder in oppsiteOrders)
        {
            if (newOrder.FilledQuantity >= newOrder.Quantity) break;
            if (CanMatch(newOrder, matchingOrder))
            {
                await ExecuteTradeAsync(newOrder, matchingOrder);
            }
            await UpdateOrderStatusAsync(newOrder);
        }
    }

    public async Task<List<Order>> GetMatchingOrders(Order newOrder)
    {
        var oppositeOrders = await _stockExchangeDbContext.Orders
                                                           .Where(order => order.SecurityId == newOrder.SecurityId &&
                                                                           order.Side != newOrder.Side &&
                                                                           (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PartiallyFilled) &&
                                                                           order.Id != newOrder.Id &&
                                                                           order.Quantity != order.FilledQuantity)
                                                           .ToListAsync();
        
        if (newOrder.Side == OrderSide.Buy)
        {
            return oppositeOrders.Where(order => order.Side == OrderSide.Sell)
                                 .OrderBy(order => order.Type == OrderType.Market ? 0 : order.Price)
                                 .ThenBy(order => order.CreatedAt)
                                 .ToList();
        }
        else
        {
            return oppositeOrders.Where(order => order.Side == OrderSide.Buy)
                                 .OrderByDescending(order => order.Type == OrderType.Market ? decimal.MaxValue : order.Price)
                                 .ThenBy(order => order.CreatedAt)
                                 .ToList();
        }
    }

    public bool CanMatch(Order order1, Order order2)
    {
        var availableQuantity1 = order1.Quantity - order1.FilledQuantity;
        var availableQuantity2 = order2.Quantity - order2.FilledQuantity;
        
        if (availableQuantity1 <= 0 || availableQuantity2 <= 0) 
            return false;
        
        if (order1.Type == OrderType.Market || order2.Type == OrderType.Market)
            return true;
        
        if (order1.Side == OrderSide.Buy && order2.Side == OrderSide.Sell)
        {
            return order1.Price >= order2.Price;
        }
        else if (order1.Side == OrderSide.Sell && order2.Side == OrderSide.Buy)
        {
            return order1.Price <= order2.Price;
        }
        
        return false;
    }

    public async Task ExecuteTradeAsync(Order order1, Order order2)
    {
        var availableQuantity1 = order1.Quantity - order1.FilledQuantity;
        var availableQuantity2 = order2.Quantity - order2.FilledQuantity;
        if (availableQuantity1 <= 0 || availableQuantity2 <= 0) return;
        
        var tradeQuantity = Math.Min(availableQuantity1, availableQuantity2);
        
        decimal tradePrice;
        if (order1.Type == OrderType.Market && order2.Type == OrderType.Limit)
        {
            tradePrice = order2.Price;
        }
        else if (order1.Type == OrderType.Limit && order2.Type == OrderType.Market)
        {
            tradePrice = order1.Price;
        }
        else if (order1.Type == OrderType.Market && order2.Type == OrderType.Market)
        {
            var security = await _stockExchangeDbContext.Securities.FindAsync(order1.SecurityId);
            tradePrice = security?.CurrentPrice ?? 0;
            
            if (tradePrice == 0)
            {
                _logger.LogWarning($"No current price available for security {order1.SecurityId}");
                return;
            }
        }
        else
        {
            tradePrice = order1.CreatedAt <= order2.CreatedAt ? order1.Price : order2.Price;
        }

        var trade = new Trade
        {
            BuyOrderId = order1.Side == OrderSide.Buy ? order1.Id : order2.Id,
            SellOrderId = order1.Side == OrderSide.Sell ? order1.Id : order2.Id,
            SecurityId = order1.SecurityId,
            Quantity = tradeQuantity,
            Price = tradePrice,
            ExecutedAt = DateTime.UtcNow
        };

        _stockExchangeDbContext.Trades.Add(trade);

        order1.FilledQuantity += tradeQuantity;
        order2.FilledQuantity += tradeQuantity;

        order1.ExecutedAt = DateTime.UtcNow;
        order2.ExecutedAt = DateTime.UtcNow;
        await UpdateOrderStatusAsync(order1);
        await UpdateOrderStatusAsync(order2);

        await _stockExchangeDbContext.SaveChangesAsync();

        var tradeMessage = new TradeMessage
        {
            TradeId = trade.Id,
            Symbol = (await _stockExchangeDbContext.Securities.FindAsync(order1.SecurityId))?.Symbol ?? "",
            BuyOrderId = trade.BuyOrderId,
            SellOrderId = trade.SellOrderId,
            BuyerClientId = order1.Side == OrderSide.Buy ? order1.ClientId : order2.ClientId,
            SellerClientId = order1.Side == OrderSide.Sell ? order1.ClientId : order2.ClientId,
            Quantity = tradeQuantity,
            Price = tradePrice,
            ExecutedAt = trade.ExecutedAt
        };

        await _producerService.PublishTradeAsync(tradeMessage);
    }

    private async Task UpdateOrderStatusAsync(Order order)
    {
        if (order.FilledQuantity == 0)
        {
            order.Status = OrderStatus.Pending;
        }
        else if (order.FilledQuantity >= order.Quantity)
        {
            order.Status = OrderStatus.Filled;
        }
        else
        {
            order.Status = OrderStatus.PartiallyFilled;
        }

        await _stockExchangeDbContext.SaveChangesAsync();
    }
}

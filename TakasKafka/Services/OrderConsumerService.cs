
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TakasKafka.Data;
using TakasKafka.Messages;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class OrderConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<OrderConsumerService> _logger;

    private readonly IServiceScopeFactory _factory;
    public OrderConsumerService(IConsumer<string, string> consumer, ILogger<OrderConsumerService> logger, IServiceScopeFactory factory)
    {
        _consumer = consumer;
        _logger = logger;
        _factory = factory;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("orders");
        _logger.LogInformation("Order consumer service started");
        
        await Task.Factory.StartNew(async () =>
        {
            try
            {
                using var scope = _factory.CreateScope();
                StockExchangeDbContext _stockExchangeDbContext = scope.ServiceProvider.GetRequiredService<StockExchangeDbContext>();
                while (!stoppingToken.IsCancellationRequested)
                {

                    try
                    {
                        //var consumeResults = _consumer.ConsumeBatch(maxBatchSize:10000, maxWaitTime: TimeSpan.FromMilliseconds(50));

                        //var messages = new List<Order>();
                        //foreach(var consumeResult in consumeResults)
                        //{
                        //    var orderMessage = JsonSerializer.Deserialize<OrderMessage>(consumeResult.Message.Value)!;
                        //    var order = new Order
                        //    {
                        //        ClientId = orderMessage.ClientId,
                        //        SecurityId = 4,
                        //        Type = Enum.Parse<OrderType>(orderMessage.OrderType),
                        //        Side = Enum.Parse<OrderSide>(orderMessage.Side),
                        //        Quantity = orderMessage.Quantity,
                        //        Price = orderMessage.Price,
                        //        Status = OrderStatus.Pending,
                        //        CreatedAt = orderMessage.Timestamp,
                        //        FilledQuantity = 0
                        //    };

                        //    messages.Add(order);
                        //}
                        //if(messages.Count > 0) { 
                        //    _stockExchangeDbContext.Orders.AddRange(messages);
                        //    _stockExchangeDbContext.SaveChanges();
                        //}
                        var consumeResult = _consumer.Consume(stoppingToken);
                        if (consumeResult?.Message?.Value != null)
                        {
                            var orderMessage = JsonSerializer.Deserialize<OrderMessage>(consumeResult.Message.Value);
                            
                            if (orderMessage != null)
                            {
                                await ProcessOrderMessage(orderMessage);
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, $"Error consuming order message: {ex.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing order message: {ex.Message}");
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatal error in order consumer: {ex.Message}");
            }
            finally
            {
                _consumer.Close();
            }
        });
    }

    private async Task ProcessOrderMessage(OrderMessage orderMessage)
    {
        using var scope = _factory.CreateScope();
        var matchingEngine = scope.ServiceProvider.GetRequiredService<MatchingEngineService>();

        _logger.LogInformation($"Processing order: {orderMessage.OrderId} - {orderMessage.Symbol} {orderMessage.Side} {orderMessage.Quantity}@{orderMessage.Price}");

        switch (orderMessage.Action.ToUpper())
        {
            case "NEW":
                await matchingEngine.ProcessNewOrderAsync(orderMessage);
                break;
            default:
                _logger.LogWarning($"Unknown order action: {orderMessage.Action}");
                break;
        }
    }
}

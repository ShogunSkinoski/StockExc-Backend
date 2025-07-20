using Confluent.Kafka;
using System.Text.Json;
using TakasKafka.Messages;
using TakasKafka.Models;

namespace TakasKafka.Services;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducerService(IProducer<string, string> producer) { _producer = producer; }

    public async Task PublishOrderAsync(OrderMessage orderMessage)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = orderMessage.Symbol,
                Value = JsonSerializer.Serialize(orderMessage)
            };

            var result = await _producer.ProduceAsync("orders", message);
        }
        catch (Exception ex)
        {
        }
    }

    public void PublishOrderInBatch(List<OrderMessage> orderMessages)
    {
        try
        {
            var messages = new List<Message<string, string>>();
            foreach(var orderMessage in orderMessages)
            {
                messages.Add(new Message<string, string>
                {
                    Key = orderMessage.Symbol,
                    Value = JsonSerializer.Serialize(orderMessage)
                }
            );
            }
            _producer.ProduceBatch("orders", messages);
        }
        catch (Exception ex)
        {
            
        }
    }

    public async Task PublishTradeAsync(TradeMessage tradeMessage)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = tradeMessage.Symbol,
                Value = JsonSerializer.Serialize(tradeMessage)
            };

            var result = await _producer.ProduceAsync("trades", message);

        }
        catch (Exception ex)
        {

        }
    }

    public async Task PublisBinanceTradeAsync(BinanceTrade tradeMessage)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = tradeMessage.TradingSymbol,
                Value = JsonSerializer.Serialize(tradeMessage)
            };

            var result = await _producer.ProduceAsync("trades", message);

        }
        catch (Exception ex)
        {

        }
    }
}

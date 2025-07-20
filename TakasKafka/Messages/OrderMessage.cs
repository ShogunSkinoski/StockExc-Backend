using TakasKafka.Models;

namespace TakasKafka.Messages;


public class OrderMessage
{
    public int OrderId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty; 
    public string Side { get; set; } = string.Empty; 
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
}

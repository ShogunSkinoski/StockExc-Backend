namespace TakasKafka.Models;

public class Order
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public int SecurityId { get; set; }
    public Security Security { get; set; } = null!;
    public OrderType Type { get; set; }
    public OrderSide Side { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public int FilledQuantity { get; set; }
    public decimal? AvgExecutionPrice { get; set; }
}

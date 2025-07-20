namespace TakasKafka.Models;
public class Trade
{
    public int Id { get; set; }
    public int BuyOrderId { get; set; }
    public Order BuyOrder { get; set; } = null!;
    public int SellOrderId { get; set; }
    public Order SellOrder { get; set; } = null!;
    public int SecurityId { get; set; }
    public Security Security { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime ExecutedAt { get; set; }
}

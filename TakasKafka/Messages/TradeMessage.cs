namespace TakasKafka.Messages;

public class TradeMessage
{
    public int TradeId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int BuyOrderId { get; set; }
    public int SellOrderId { get; set; }
    public string BuyerClientId { get; set; } = string.Empty;
    public string SellerClientId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime ExecutedAt { get; set; }
}

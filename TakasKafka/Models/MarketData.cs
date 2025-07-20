namespace TakasKafka.Models;
public class MarketData
{
    public int Id { get; set; }
    public int SecurityId { get; set; }
    public Security Security { get; set; } = null!;
    public decimal Price { get; set; }
    public int Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public MarketDataType Type { get; set; }
}

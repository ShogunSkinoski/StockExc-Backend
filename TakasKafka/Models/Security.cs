namespace TakasKafka.Models;
/// <summary>
/// A fungible, negotiable financial instrument that holds some type of monetary value
/// </summary>
public class Security
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal PreviousPrice { get; set; }
    public long Volume { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;

    public List<Order> Orders { get; set; } = new();
    public List<Trade> Trades { get; set; } = new();
}
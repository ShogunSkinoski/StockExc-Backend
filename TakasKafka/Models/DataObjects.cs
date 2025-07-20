using System.ComponentModel;

namespace TakasKafka.Models;

/// <summary>
/// A market order is an instruction by an investor to a broker to buy or sell stock shares, bonds, or other assets at the best available price in the current financial market.
/// </summary
public enum OrderType
{
    [Description("A market order is an instruction to buy or sell a security immediately at the current price.")]
    Market,
    [Description("A limit order is an instruction to buy or sell only at a price specified by the investor.")]
    Limit
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderStatus
{
    Pending,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}


public enum MarketDataType
{
    Trade,
    Quote,
    MarketOpen,
    MarketClose
} 
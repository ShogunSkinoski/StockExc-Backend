using System.Security.Cryptography.X509Certificates;

namespace TakasKafka.Models;
using Newtonsoft.Json;
using System.Text.Json;

public class BinanceTrade
{
    public string EventType { get; set; }

    public long EventTime { get; set; }

    public string TradingSymbol { get; set; }

    public long TradeId { get; set; }

    public string Price { get; set; }

    public string Quantity { get; set; }

    public long TradeCompletedTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public static BinanceTrade MapFromDictionary(Dictionary<string, object> dict)
    {
        var jsonDict = dict.ToDictionary(
        kvp => kvp.Key,
        kvp => (JsonElement)kvp.Value
    );

        return new BinanceTrade
        {
            EventType = jsonDict["e"].GetString(),
            EventTime = jsonDict["E"].GetInt64(),
            TradingSymbol = jsonDict["s"].GetString(),
            TradeId = jsonDict["t"].GetInt64(),
            Price = jsonDict["p"].GetString(),
            Quantity = jsonDict["q"].GetString(),
            TradeCompletedTime = jsonDict["T"].GetInt64()
        };

    }
}
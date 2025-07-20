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

    public static BinanceTrade? MapFromDescriptiveDictionary(Dictionary<string, object>? dict)
    {
        if (dict == null || dict.Count == 0)
        {
            return null;
        }

        try
        {
            var requiredKeys = new[] { "TradingSymbol", "TradeId", "Price", "Quantity" };
            foreach (var key in requiredKeys)
            {
                if (!dict.ContainsKey(key))
                {
                    return null; 
                }
            }

            if (!TryGetLongValue(dict, "TradeId", out var tradeId) || tradeId <= 0)
            {
                return null;
            }

            return new BinanceTrade
            {
                EventType = TryGetStringValue(dict, "EventType"),
                Price = TryGetStringValue(dict, "Price"),
                Quantity = TryGetStringValue(dict, "Quantity"),
                EventTime = TryGetLongValue(dict, "EventTime", out var eventTime) ? eventTime : 0,
                TradeCompletedTime = TryGetLongValue(dict, "TradeCompletedTime", out var completedTime) ? completedTime : 0,
                CreatedAt = DateTime.Now,
                TradeId = tradeId,
                TradingSymbol = TryGetStringValue(dict, "TradingSymbol")
            };
        }
        catch (Exception ex)
        {
            return null;
        }
    }
    private static string? TryGetStringValue(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                string str when !string.IsNullOrEmpty(str) => str,
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                null => null,
                _ => value.ToString()
            };
        }
        return null;
    }

    private static bool TryGetLongValue(Dictionary<string, object> dict, string key, out long result)
    {
        result = 0;

        if (!dict.TryGetValue(key, out var value))
        {
            return false;
        }

        return value switch
        {
            long longValue => (result = longValue) >= 0,
            int intValue => (result = intValue) >= 0,
            string str when long.TryParse(str, out var parsed) => (result = parsed) >= 0,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number =>
                jsonElement.TryGetInt64(out result),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String =>
                long.TryParse(jsonElement.GetString(), out result),
            _ => false
        };
    }

    private static long TryGetLongValue(Dictionary<string, object> dict, string key)
    {
        TryGetLongValue(dict, key, out var result);
        return result;
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(TradingSymbol) &&
               !string.IsNullOrEmpty(Price) &&
               !string.IsNullOrEmpty(Quantity) &&
               TradeId > 0 &&
               decimal.TryParse(Price, out var price) && price > 0 &&
               decimal.TryParse(Quantity, out var quantity) && quantity > 0;
    }
}
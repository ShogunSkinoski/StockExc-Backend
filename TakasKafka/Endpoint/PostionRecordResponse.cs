namespace TakasKafka.Endpoint;

public sealed record PostionRecordResponse
(
    List<Dictionary<string, object>> TradePositions
);

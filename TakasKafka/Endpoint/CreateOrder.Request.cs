namespace TakasKafka.Endpoint;

public record CreateOrderRequest(
    string ClientId,
    string Symbol,
    string OrderType,
    string Side,
    int Quantity,
    decimal Price,
    bool SendBatch = false
);

namespace TakasKafka.Endpoint;

public record GetPositionsRequest
(
    string Symbol,
    string ClientId
);

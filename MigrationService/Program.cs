using TakasKafka.Data;
using MigrationService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));


builder.AddNpgsqlDbContext<StockExchangeDbContext>(connectionName: "postgres-container");

var host = builder.Build();
host.Run();

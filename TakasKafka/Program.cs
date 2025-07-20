
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TakasKafka.Data;
using TakasKafka.DataHub;
using TakasKafka.Endpoint;
using TakasKafka.Messages;
using TakasKafka.Services;

namespace TakasKafka;

public class Program
{


    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddAuthorization();

        builder.Services.AddOpenApi();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("GetorPost",
                builder =>
                builder.AllowAnyOrigin().WithMethods("GET", "POST")
                .AllowAnyHeader());
        });

        builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);


        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IConsumer<string, string>>(provider =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = builder.Configuration.GetConnectionString("kafka-container"),
                GroupId = "stock-exchange-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };
            return new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    var logger = provider.GetRequiredService<ILogger<Program>>();
                    logger.LogError($"Kafka consumer error: {e.Reason}");
                })
                .Build();
        });
        builder.Services.AddSingleton<IProducer<string, string>>(provider =>
        {
            var config = new ProducerConfig
            {
                BootstrapServers = builder.Configuration.GetConnectionString("kafka-container"),
                MessageTimeoutMs = 5000,
                RequestTimeoutMs = 5000,
                RetryBackoffMs = 100,
                MessageSendMaxRetries = 3,
                SocketKeepaliveEnable = true,
                SocketTimeoutMs = 30000,
            };
            return new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    var logger = provider.GetRequiredService<ILogger<Program>>();
                    logger.LogError($"Kafka producer error: {e.Reason}");
                })
                .Build();
        });
        builder.Services.AddSingleton<IAdminClient>(provider =>
        {
            var config = new AdminClientConfig {
                Acks = Acks.Leader,
                BootstrapServers = builder.Configuration.GetConnectionString("kafka-container"),
                RetryBackoffMs = 100,
                SocketKeepaliveEnable = true,
                SocketTimeoutMs = 30000,
            };
            return new AdminClientBuilder(config).Build();
        });

        builder.Services.AddDbContext<StockExchangeDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("postgres")));
        builder.Services.AddHostedService<TradeConsumeService>();
        builder.Services.AddScoped<MatchingEngineService>();
        builder.Services.AddScoped<MarketDataService>();

        builder.Services.AddHostedService<OrderConsumerService>();
        builder.Services.AddScoped<KafkaProducerService>();
        builder.Services.AddHostedService<BinanceTradeProducer>();
        builder.Services.AddSignalR();
        var app = builder.Build();
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        using (var scope = app.Services.CreateScope())
        {
            var adminClient = scope.ServiceProvider.GetRequiredService<IAdminClient>();
            try
            {
                await adminClient.CreateTopicsAsync(new TopicSpecification[]
                {
                    new TopicSpecification
                    {
                        Name="orders",
                        ReplicationFactor=1,
                        NumPartitions=1
                    },
                    new TopicSpecification
                    {
                        Name="trades",
                        ReplicationFactor=1,
                        NumPartitions=1
                    }
                });
            }
            catch (CreateTopicsException ex) {
                Console.WriteLine(ex.Results[0].Topic + " : " + ex.Results[0].Error.Reason);
            }
        }
       using(var scope = app.Services.CreateScope())
       {
           var context = scope.ServiceProvider.GetRequiredService<StockExchangeDbContext>();
           await SeedDataAsync(context);

       }
        app.MapHub<MarketDataHub>("/marketDataHub");

        app.MapPost("/orders", async (List<CreateOrderRequest> orderRequests, KafkaProducerService producerService) =>
        {
            try
            {
                var orderMessages = new List<OrderMessage>();
                Random random = new Random();
                foreach (var orderRequest in orderRequests)
                {
                    orderMessages.Add(new OrderMessage
                    {
                        OrderId = 0,
                        ClientId = orderRequest.ClientId,
                        Symbol = orderRequest.Symbol,
                        OrderType = orderRequest.OrderType,
                        Side = orderRequest.Side,
                        Quantity = orderRequest.Quantity,
                        Price = orderRequest.Price,
                        Timestamp = DateTime.UtcNow,
                        Action = "NEW"
                    }
                    );

                }
                producerService.PublishOrderInBatch(orderMessages);
                return Results.Ok(new { Message = "Order submitted successfully" });
            }
            catch
            {
                return Results.InternalServerError();
            }

        });
        app.MapGet("/positions", async ([FromQuery]string clientId, StockExchangeDbContext stockExchangeDbContext) =>
        {
            try
            {

                var longTrades = stockExchangeDbContext.Trades.Where(trade => trade.BuyOrder.ClientId == clientId)
                                                              .Include(trade => trade.Security)
                                                              .GroupBy(trade => trade.Security.Symbol)
                                                              .ToList();
                var shortTrades = stockExchangeDbContext.Trades.Where(trade => trade.SellOrder.ClientId == clientId)
                                                                .Include(trade => trade.Security)
                                                                .GroupBy(trade => trade.Security.Symbol)
                                                                .ToList();
                var longTradeKeys = new List<string>();
                var shortTradeKeys = new List<string>();

                longTrades.ForEach(trade =>
                {
                    longTradeKeys.Add(trade.Key);
                });

                shortTrades.ForEach(trade =>
                {
                    shortTradeKeys.Add(trade.Key);
                });

                Dictionary<string, Dictionary<string, object>> tradePositions = new Dictionary<string, Dictionary<string, object>>();

                
                longTradeKeys.ForEach(key =>
                {
                    if (!tradePositions.Keys.Contains(key))
                    { 
                        tradePositions.TryAdd(key, new Dictionary<string, object>());
                        tradePositions[key].Add("symbol", key);
                    }

                    var longPositionCount = longTrades.First(trade => trade.Key == key).Count();

                    if (shortTradeKeys.Contains(key))
                    {
                    
                        var shortPositionCount = shortTrades.First(trade => trade.Key == key).Count();
                        var netPositionCount = longPositionCount - shortPositionCount;

                        tradePositions[key].Add("longTradePositions", longPositionCount);
                        tradePositions[key].Add("shortTradePositions", shortPositionCount);
                        tradePositions[key].Add("netTradePositions", netPositionCount);

                    }else
                    {
                        tradePositions[key].Add("longTradePositions", longPositionCount);
                        tradePositions[key].Add("shortTradePositions", 0);
                        tradePositions[key].Add("netTradePositions", longPositionCount);
                    }
                });

                shortTradeKeys.ForEach(key =>
                {
                    if (!tradePositions.Keys.Contains(key)) tradePositions.TryAdd(key, new Dictionary<string, object>());

                    if (tradePositions.TryGetValue(key, out var tradePosition))
                    {
                        if(tradePosition.Keys.Count == 0)
                        {
                            tradePositions.TryAdd(key, new Dictionary<string, object>());
                            var shortPositionCount = shortTrades.First(trade => trade.Key == key).Count();
                            tradePositions[key].Add("symbol", key);
                            tradePositions[key].Add("longTradePositions", 0);
                            tradePositions[key].Add("shortTradePositions", shortPositionCount);
                            tradePositions[key].Add("netTradePositions", -1 * shortPositionCount);
                        }
                    }
                });

                var tradesPositionList = new List<Dictionary<string, object>>();
                foreach (var tradePosition in tradePositions.Values)
                {
                    tradesPositionList.Add(tradePosition);
                }
                return Results.Ok(new PostionRecordResponse (tradesPositionList));
            }
            catch
            {
                return Results.InternalServerError();
            }
        });

        app.MapDefaultEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors(x => x
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .SetIsOriginAllowed(origin => true) // allow any origin
                                                       //.WithOrigins("https://localhost:44351")); // Allow only this origin can also have multiple origins separated with comma
                   .AllowCredentials()); // allow credentials
        }

        app.Run();
    }

    static async Task SeedDataAsync(StockExchangeDbContext context)
    {
        if (!await context.Securities.AnyAsync())
        {
            var securities = new[]
            {
            new TakasKafka.Models.Security { Symbol = "AAPL", CompanyName = "Apple Inc.", CurrentPrice = 150.00m, PreviousPrice = 148.50m, Volume = 0,  IsActive = true, LastUpdated = DateTime.UtcNow },
            new TakasKafka.Models.Security { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", CurrentPrice = 2800.00m, PreviousPrice = 2750.00m, Volume = 0, IsActive = true, LastUpdated = DateTime.UtcNow },
            new TakasKafka.Models.Security { Symbol = "MSFT", CompanyName = "Microsoft Corporation", CurrentPrice = 300.00m, PreviousPrice = 298.00m, Volume = 0, IsActive = true, LastUpdated = DateTime.UtcNow },
            new TakasKafka.Models.Security { Symbol = "TSLA", CompanyName = "Tesla Inc.", CurrentPrice = 800.00m, PreviousPrice = 790.00m, Volume = 0, IsActive = true, LastUpdated = DateTime.UtcNow },
            new TakasKafka.Models.Security { Symbol = "AMZN", CompanyName = "Amazon.com Inc.", CurrentPrice = 3200.00m, PreviousPrice = 3150.00m, Volume = 0, IsActive = true, LastUpdated = DateTime.UtcNow },
            new TakasKafka.Models.Security { Symbol = "BTCUSDT", CompanyName = "Bitcoin USDT", CurrentPrice = 0, PreviousPrice = 0, Volume = 0, IsActive = true, LastUpdated = DateTime.UtcNow }

        };

            context.Securities.AddRange(securities);
            await context.SaveChangesAsync();
        }
    }
}

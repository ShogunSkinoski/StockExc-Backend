var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("kafka-container")
                    .WithKafkaUI();


builder.AddProject<Projects.TakasKafka>("takaskafka")
        .WaitFor(kafka)
        .WithReference(kafka);

builder.Build().Run();

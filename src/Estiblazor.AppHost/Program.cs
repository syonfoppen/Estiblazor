using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("Redis");

builder.AddProject<Estiblazor_UI>("estiblazor-ui")
    .WithReference(redis);

builder.Build().Run();

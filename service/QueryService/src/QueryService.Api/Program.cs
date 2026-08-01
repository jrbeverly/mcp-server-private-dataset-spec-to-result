using QueryService.Api.Endpoints.Discovery;
using QueryService.Api.Endpoints.Aggregation;
using QueryService.Api.Endpoints.Metadata;
using QueryService.Api.Endpoints.DrillDown;
using QueryService.Api.Endpoints.RawEvidence;
using QueryService.Api.Middleware;
using QueryService.Domain.Data;
using QueryService.Domain.Services;
using QueryService.Infrastructure;
using ServiceConfiguration;

var builder = WebApplication.CreateBuilder(args);

// Configuration — environment-first (12-factor)
var config = ServiceBootstrap.Load();
builder.Services.AddSingleton(config);

// Domain services — ALL analytical logic lives here
// Registered as scoped: one instance per request
builder.Services.AddSingleton(config.DatasetVersion);
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IAggregateService, AggregateService>();
builder.Services.AddScoped<IMetadataExplorationService, MetadataExplorationService>();
builder.Services.AddScoped<IDrillDownService, DrillDownService>();
builder.Services.AddScoped<IRawEvidenceService, RawEvidenceService>();
builder.Services.AddScoped<IDatasetVersionResolver, DatasetVersionResolver>();

// Infrastructure
builder.Services.AddSingleton<IServingStoreRepository>(sp =>
{
    var connString = sp.GetRequiredService<ServiceBootstrap>().Storage.ServingStore.ConnectionString;
    return new PostgresServingStoreRepository(connString);
});
builder.Services.AddSingleton<ServingVersionLoader>(sp =>
{
    var repo = sp.GetRequiredService<IServingStoreRepository>();
    var root = Environment.GetEnvironmentVariable("ARTIFACT_ROOT_PATH")
        ?? Path.Combine(Path.GetTempPath(), "processing-artifacts");
    return new ServingVersionLoader(repo, root);
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<StaticTokenMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();

// Route mapping — one endpoint per file, per the Minimal API pattern
var v1 = app.MapGroup("/api/v1");

DiscoveryEndpoint.Registration.Map(v1);
AggregateEndpoint.Registration.Map(v1);
MetadataExplorationEndpoint.Registration.Map(v1);
DrillDownEndpoint.Registration.Map(v1);
RawEvidenceEndpoint.Registration.Map(v1);

app.Run();

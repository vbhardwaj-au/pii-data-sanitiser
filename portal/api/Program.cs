using Microsoft.EntityFrameworkCore;
using DAL;
using DAL.Repositories;
using API.Mapping;
using API.Services;
using API.Middleware;
using API.Filters;
using API.Authorization;
using API.Hubs;
using MediatR;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add global validation filter
    options.Filters.Add<ValidateModelFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PII Data Sanitizer Portal API",
        Version = "v1",
        Description = "API for managing pipeline orchestration and data obfuscation operations",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@company.com"
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License"
        }
    });

    // Include XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add security definition for potential future authentication
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Group endpoints by controller
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
    c.DocInclusionPredicate((name, api) => true);

    // Add example responses
    c.EnableAnnotations();
});

// Add Entity Framework
builder.Services.AddDbContext<PortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PortalDb")));

// Hangfire removed - using distributed agents instead

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductConnectionStringRepository, ProductConnectionStringRepository>();
builder.Services.AddScoped<IPipelineStageRepository, PipelineStageRepository>();
builder.Services.AddScoped<IProductPipelineConfigurationRepository, ProductPipelineConfigurationRepository>();
builder.Services.AddScoped<IPipelineStageScriptRepository, PipelineStageScriptRepository>();
builder.Services.AddScoped<IScriptExecutionLogRepository, ScriptExecutionLogRepository>();
builder.Services.AddScoped<IPipelineExecutionRepository, PipelineExecutionRepository>();
builder.Services.AddScoped<IObfuscationAgentRepository, ObfuscationAgentRepository>();
builder.Services.AddScoped<IObfuscationJobRepository, ObfuscationJobRepository>();

// Add Services
builder.Services.AddScoped<IConnectionStringEncryptionService, ConnectionStringEncryptionService>();
builder.Services.AddScoped<IConnectionTestingService, ConnectionTestingService>();
builder.Services.AddScoped<IPipelineStageSeederService, PipelineStageSeederService>();
builder.Services.AddScoped<ISqlScriptValidatorService, SqlScriptValidatorService>();
builder.Services.AddScoped<IScriptExecutionLoggingService, ScriptExecutionLoggingService>();
// Add Pipeline Services
builder.Services.AddScoped<IPipelineOrchestrationService, PipelineOrchestrationService>();
builder.Services.AddScoped<IStageExecutionService, StageExecutionService>();
builder.Services.AddScoped<IPipelineStateManagerService, PipelineStateManagerService>();
builder.Services.AddScoped<IPipelineExecutionService, PipelineExecutionService>();

// Add Azure Storage Services
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Add Caching
builder.Services.AddMemoryCache();

// Add Distributed Agent Services
builder.Services.AddSingleton<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IAgentManagementService, AgentManagementService>();
builder.Services.AddScoped<IJobDistributionService, JobDistributionService>();
builder.Services.AddScoped<IAgentDiscoveryService, AgentDiscoveryService>();
builder.Services.AddScoped<IWorkloadDistributionService, WorkloadDistributionService>();
builder.Services.AddScoped<IResultAggregationService, ResultAggregationService>();
// TestJobService removed - not needed
builder.Services.AddScoped<IRealtimeMonitoringService, RealtimeMonitoringService>();
builder.Services.AddScoped<ILogStreamingService, LogStreamingService>();

// Add Background Services
builder.Services.AddHostedService<AgentHealthMonitorService>();
builder.Services.AddHostedService<JobProcessingService>();
// SimplePipelineProcessor removed - jobs created directly on pipeline trigger

// Jobs removed - using distributed agents instead

// Add API Key Authentication
builder.Services.AddApiKeyAuthentication(builder.Configuration);

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:7002", "http://localhost:3000", "https://localhost:7002")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "X-API-Key");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PII Data Sanitizer Portal API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
        c.ConfigObject.AdditionalItems.Add("syntaxHighlight", new Dictionary<string, object>
        {
            ["activated"] = true,
            ["theme"] = "monokai"
        });
    });
}

// Add error handling middleware (should be first)
app.UseErrorHandling();

// CORS must be before authentication to handle preflight requests
app.UseCors("AllowAll");

// Add API Key Authentication middleware
app.UseApiKeyAuthentication();

// Hangfire dashboard removed - using distributed agents instead

app.UseAuthorization();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

// Configure the application to listen on port 7001
// Allow URLs to be configured via environment variables or launchSettings.json
// If ASPNETCORE_URLS is not set, use default
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    app.Urls.Clear();
    app.Urls.Add("http://0.0.0.0:7001");  // Listen on all interfaces for Docker access
}

// Seed default data and initialize services
using (var scope = app.Services.CreateScope())
{
    var seederService = scope.ServiceProvider.GetRequiredService<IPipelineStageSeederService>();
    await seederService.SeedDefaultStagesAsync();
    
    // Initialize Azure Blob containers
    var blobStorageService = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
    await blobStorageService.InitializeContainersAsync();
}

app.Run();

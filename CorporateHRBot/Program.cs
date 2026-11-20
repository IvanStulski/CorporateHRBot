using Azure.Identity;
using CorporateHRBot.Extensions;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
using CorporateHRBot.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

builder.Services.AddControllers();
builder.Services.AddAntiforgery();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCustomOptions();
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisSection = builder.Configuration.GetSection("Redis");

    options.Configuration = redisSection["Endpoint"]
        ?? throw new InvalidOperationException("Redis:ConnectionString is missing.");

    options.InstanceName = redisSection["Name"]
        ?? "CorporateHRBot:";
});

builder.Services.AddDbContext<HrRagDbContext>(options =>
{
    var connection = builder.Configuration["HRDbConnection"];
    options.UseSqlServer(connection);
});
builder.Services.AddTransient<IDocumentsRepository, DocumentsRepository>();
builder.Services.AddCustomService();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapDocumentEndpoints();

app.MapMcp();
try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}


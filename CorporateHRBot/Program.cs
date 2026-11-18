using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
using CorporateHRBot.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();

    string endpointUri = configuration["AzureOpenAI:Endpoint"]
                         ?? throw new InvalidOperationException("No AzureOpenAI:Endpoint");
    string apiKey = configuration["AzureOpenAI:ApiKey"]
                    ?? throw new InvalidOperationException("No AzureOpenAI:ApiKey");

    return new AzureOpenAIClient(new Uri(endpointUri), new AzureKeyCredential(apiKey));
});
builder.Services.AddDbContext<HrRagDbContext>(options =>
{
    var connection = builder.Configuration["HRDbConnection"];
    options.UseSqlServer(connection);
});
builder.Services.AddTransient<IDocumentsRepository, DocumentsRepository>();
builder.Services.AddTransient<IUnitOfWork, UnitOfWork>();
builder.Services.AddTransient<IModelEvaluatorToolService, ModelEvaluatorToolService>();
builder.Services.AddTransient<IHrRagService, HrRagService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();
app.MapDocumentEndpoints();

app.Run();

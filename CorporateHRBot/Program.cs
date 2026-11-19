using Azure.Identity;
using CorporateHRBot.Extensions;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
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
builder.Services.AddCustomOptions();

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

app.MapControllers();
app.MapDocumentEndpoints();

app.Run();

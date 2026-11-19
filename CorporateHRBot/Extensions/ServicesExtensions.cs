using Azure;
using Azure.AI.OpenAI;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Options;
using CorporateHRBot.Services;
using Microsoft.Extensions.Options;

namespace CorporateHRBot.Extensions;

public static class ServicesExtensions
{
    public static void AddCustomService(this IServiceCollection services)
    {
        services.AddTransient(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<AzureOpenAIOptions>>()
                .Value;

            return new AzureOpenAIClient(
                new Uri(options.Endpoint),
                new AzureKeyCredential(options.ApiKey));
        });
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddTransient<IModelEvaluatorToolService, ModelEvaluatorToolService>();
        services.AddTransient<IHrRagService, HrRagService>();
    }
}

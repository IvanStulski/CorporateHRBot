using CorporateHRBot.Options;

namespace CorporateHRBot.Extensions;

public static class OptionsExtensions
{
    public static void AddCustomOptions(this IServiceCollection services)
    {
        services.AddOptions<AzureOpenAIOptions>()
            .BindConfiguration("AzureOpenAI")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AIRequestsOptions>()
            .BindConfiguration("AIRequestsOptions")
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}

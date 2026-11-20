using CorporateHRBot.Options;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

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

        services
            .AddOptions<RedisCacheOptions>()
            .Configure<IOptions<RedisOptions>>((redisCacheOptions, redisOptions) =>
            {
                RedisOptions options = redisOptions.Value;

                redisCacheOptions.Configuration = options.ConnectionString;
                redisCacheOptions.InstanceName = options.Name;
            });
    }
}

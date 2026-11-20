using Azure.AI.OpenAI;
using CorporateHRBot.Entities;
using CorporateHRBot.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CorporateHRBot.Services;

public interface IModelEvaluatorToolService
{
    Task<ChatClient> GetChatClientByComplexity(string input, CancellationToken token);
}

public sealed class ModelEvaluatorToolService : IModelEvaluatorToolService
{
    private const int AbsoluteExpirationRelativeToNow = 12;
    private const int RedisSlidingExpiration = 2;
    private const string EvaluateModelComplexityToolName = "EvaluateModelComplexity";

    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly IOptions<AIRequestsOptions> _azureOpenAIOptions;
    private readonly IDistributedCache _cache;

    public ModelEvaluatorToolService(
        AzureOpenAIClient azureOpenAIClient,
        IOptions<AIRequestsOptions> azureOpenAIOptions,
        IDistributedCache cache)
    {
        _azureOpenAIClient = azureOpenAIClient;
        _azureOpenAIOptions = azureOpenAIOptions;
        _cache = cache;
    }

    public async Task<ChatClient> GetChatClientByComplexity(string input, CancellationToken token)
    {
        var complexity = await GetRequestComplexity(input, token);
        
        var targetModelName = complexity switch
        {
            Complexity.Low => _azureOpenAIOptions.Value.LowComplexityModel,
            Complexity.Middle => _azureOpenAIOptions.Value.MiddleComplexityModel,
            Complexity.High => _azureOpenAIOptions.Value.HighComplexityModel,
            _ => _azureOpenAIOptions.Value.LowComplexityModel
        };

        return _azureOpenAIClient.GetChatClient(targetModelName);
    }

    private async Task<Complexity> GetRequestComplexity(
    string input,
    CancellationToken cancellationToken)
    {
        var chatClient = _azureOpenAIClient.GetChatClient(
            _azureOpenAIOptions.Value.BaseEvaluateModel);

        var conversation = new List<ChatMessage>
        {
            new SystemChatMessage("""
                Use the EvaluateModelComplexity tool exactly once.

                Pass the user request as the prompt argument.
                Do not determine complexity yourself.
                """),

            new UserChatMessage(input)
        };

        var evaluateComplexityTool = ChatTool.CreateFunctionTool(
            functionName: EvaluateModelComplexityToolName,
            functionDescription: """
            Evaluates a prompt and returns its complexity:
            0 = low, 1 = middle, 2 = high.
            """,
            functionParameters: BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "input": {
                  "type": "string",
                  "description": "The user prompt to evaluate."
                }
              },
              "required": ["input"],
              "additionalProperties": false
            }
            """));

        var options = new ChatCompletionOptions
        {
            Temperature = 0.0f,
            Tools = { evaluateComplexityTool }
        };

        try
        {
            var response = await chatClient.CompleteChatAsync(
                conversation,
                options,
                cancellationToken: cancellationToken);

            if (response.Value.FinishReason != ChatFinishReason.ToolCalls)
            {
                return await EvaluateModelComplexityToolAsync(
                    input,
                    cancellationToken);
            }

            foreach (ChatToolCall toolCall in response.Value.ToolCalls)
            {
                if (toolCall.FunctionName != EvaluateModelComplexityToolName)
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(
                    toolCall.FunctionArguments);

                string prompt = document.RootElement
                    .GetProperty("input")
                    .GetString()
                    ?? input;

                return await EvaluateModelComplexityToolAsync(
                    prompt,
                    cancellationToken);
            }

            return Complexity.Middle;
        }
        catch
        {
            return Complexity.Middle;
        }
    }

    private async Task<Complexity> EvaluateModelComplexityToolAsync(
    string prompt,
    CancellationToken cancellationToken)
    {
        var cacheKey = CreateComplexityCacheKey(prompt);

        try
        {
            string? cachedValue = await _cache.GetStringAsync(
                cacheKey,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(cachedValue) &&
                Enum.TryParse(
                    cachedValue,
                    ignoreCase: true,
                    out Complexity cachedComplexity))
            {
                return cachedComplexity;
            }
        }
        catch
        {
        }

        var complexity = await EvaluateComplexityByAiAsync(
            prompt,
            cancellationToken);

        try
        {
            await _cache.SetStringAsync(
                cacheKey,
                complexity.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(AbsoluteExpirationRelativeToNow),
                    SlidingExpiration = TimeSpan.FromHours(RedisSlidingExpiration)
                },
                cancellationToken);
        }
        catch
        {
            
        }

        return complexity;
    }

    private async Task<Complexity> EvaluateComplexityByAiAsync(
    string prompt,
    CancellationToken cancellationToken)
    {
        ChatClient chatClient = _azureOpenAIClient.GetChatClient(
            _azureOpenAIOptions.Value.BaseEvaluateModel);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
                Evaluate the complexity of the user request.

                Return JSON only, without Markdown or explanation.

                Valid values:
                {"complexity": 0} - low complexity.
                {"complexity": 1} - middle complexity.
                {"complexity": 2} - high complexity.
                """),

            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0.0f,
            MaxOutputTokenCount = 20
        };

        try
        {
            var response = await chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken: cancellationToken);

            string jsonResponse = response.Value.Content[0].Text.Trim();

            using JsonDocument document = JsonDocument.Parse(jsonResponse);

            if (!document.RootElement.TryGetProperty(
                    "complexity",
                    out JsonElement complexityElement))
            {
                return Complexity.Middle;
            }

            int complexityValue = complexityElement.ValueKind switch
            {
                JsonValueKind.Number => complexityElement.GetInt32(),
                JsonValueKind.String when int.TryParse(
                    complexityElement.GetString(),
                    out int parsedComplexity) => parsedComplexity,

                _ => 1
            };

            return complexityValue switch
            {
                0 => Complexity.Low,
                1 => Complexity.Middle,
                2 => Complexity.High,
                _ => Complexity.Middle
            };
        }
        catch
        {
            return Complexity.Middle;
        }
    }

    private static string CreateComplexityCacheKey(string input)
    {
        string normalizedInput = input
            .Trim()
            .ToLowerInvariant()
            .Replace("\r\n", "\n")
            .Replace("\n", " ");

        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(normalizedInput));

        return $"complexity:{Convert.ToHexString(hash)}";
    }
}
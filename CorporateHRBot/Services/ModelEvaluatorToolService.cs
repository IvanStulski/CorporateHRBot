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
        var cacheKey = CreateComplexityCacheKey(input);
        var cachedValue = await _cache.GetStringAsync(cacheKey, token);
        var complexity = await GetRequestComplexity(input, cacheKey, cachedValue, token);
        
        if (string.IsNullOrWhiteSpace(cachedValue))
        {
            await _cache.SetStringAsync(
            cacheKey,
            complexity.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                SlidingExpiration = TimeSpan.FromHours(2)
            }, token);
        }
        
        var targetModelName = complexity switch
        {
            Complexity.Low => _azureOpenAIOptions.Value.LowComplexityModel,
            Complexity.Middle => _azureOpenAIOptions.Value.MiddleComplexityModel,
            Complexity.High => _azureOpenAIOptions.Value.HighComplexityModel,
            _ => _azureOpenAIOptions.Value.LowComplexityModel
        };

        return _azureOpenAIClient.GetChatClient(targetModelName);
    }

    private async Task<Complexity> GetRequestComplexity(string input, string cacheKey, string? cachedValue, CancellationToken token)
    {
        var chatClient = _azureOpenAIClient.GetChatClient(_azureOpenAIOptions.Value.BaseEvaluateModel);
        var conversation = new List<ChatMessage>
        {
            new SystemChatMessage(_azureOpenAIOptions.Value.ComplexityEvaluationPrompt),
            new UserChatMessage(input)
        };
        var getRequestComplexityTool = ChatTool.CreateFunctionTool(
           functionName: EvaluateModelComplexityToolName,
           functionDescription: "Get complexity of user query.",
           functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "input": {
                        "type": "string",
                        "description": "User query."
                    }
                },
                "required": ["input"]
            }
            """)
        );
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0.0f,
            Tools = { getRequestComplexityTool }
        };

        try
        {
            var response = await chatClient.CompleteChatAsync(conversation, options, cancellationToken: token);

            if (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                conversation.Add(new AssistantChatMessage(response.Value));

                foreach (var toolCall in response.Value.ToolCalls)
                {
                    if (toolCall.FunctionName == EvaluateModelComplexityToolName)
                    {
                        using var document = JsonDocument.Parse(toolCall.FunctionArguments);
                        if (document.RootElement.TryGetProperty("input", out var userInputElement))
                        {
                            if (!string.IsNullOrWhiteSpace(cachedValue) &&
                                Enum.TryParse(cachedValue, ignoreCase: true, out Complexity cachedComplexity))
                            {
                                return cachedComplexity;
                            }
                            
                            conversation.Add(new ToolChatMessage(toolCall.Id, $"Project result of input carefully in integer format: {userInputElement}"));
                        }
                    }
                }

                response = await chatClient.CompleteChatAsync(conversation, options, cancellationToken: token);
            }
            string jsonResponse = response.Value.Content[0].Text.Trim();

            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("complexity", out var complexityElement))
            {
                int intComplexity = 0;

                if (complexityElement.ValueKind == JsonValueKind.String)
                {
                    intComplexity = Convert.ToInt32(complexityElement.GetString());
                }
                else if (complexityElement.ValueKind == JsonValueKind.Number)
                {
                    intComplexity = complexityElement.GetInt32();
                }
                
                return intComplexity switch
                {
                    0 => Complexity.Low,
                    1 => Complexity.Middle,
                    2 => Complexity.High,
                    _ => Complexity.Low
                };
            }
            return Complexity.Middle;
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
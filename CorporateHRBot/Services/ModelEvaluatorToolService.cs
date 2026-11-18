using Azure.AI.OpenAI;
using CorporateHRBot.Entities;
using OpenAI.Chat;
using System.Text.Json;

namespace CorporateHRBot.Services;

public interface IModelEvaluatorToolService
{
    Task<ChatClient> GetChatClientByComplexity(string input, CancellationToken token);
}

public sealed class ModelEvaluatorToolService : IModelEvaluatorToolService
{
    private const string EvaluateModel = "gpt-4o-mini";
    private const string LowComplexityModel = "gpt-4o-mini";
    private const string MiddleComplexityModel = "gpt-5-mini";
    private const string HighComplexityModel = "claude-3-5-sonnet";

    private readonly AzureOpenAIClient _azureOpenAIClient;

    public ModelEvaluatorToolService(AzureOpenAIClient azureOpenAIClient)
    {
        _azureOpenAIClient = azureOpenAIClient;
    }

    public async Task<ChatClient> GetChatClientByComplexity(string input, CancellationToken token)
    {
        var complexity = await GetRequestComplexity(input, token);
        var targetModelName = complexity switch
        {
            Complexity.Low => LowComplexityModel,
            Complexity.Middle => MiddleComplexityModel,
            Complexity.High => HighComplexityModel,
            _ => LowComplexityModel
        };

        return _azureOpenAIClient.GetChatClient(targetModelName);
    }

    private async Task<Complexity> GetRequestComplexity(string input, CancellationToken token)
    {
        var chatClient = _azureOpenAIClient.GetChatClient(EvaluateModel);
        var conversation = new List<ChatMessage>
        {
            new SystemChatMessage("Evaluate complexity of this request. Return response in JSON format like this: {\"complexity\": \"0\"} (low) or {\"complexity\": \"1\"} (middle) or {\"complexity\": \"2\"}\" (high). Necessarily use available tool."),
            new UserChatMessage(input)
        };
        var getRequestComplexityTool = ChatTool.CreateFunctionTool(
           functionName: "EvaluateModelComplexity",
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
            string jsonResponse = response.Value.Content[0].Text.Trim();

            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("complexity", out var complexityElement))
            {
                int intComplexity = complexityElement.GetInt32();
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
}
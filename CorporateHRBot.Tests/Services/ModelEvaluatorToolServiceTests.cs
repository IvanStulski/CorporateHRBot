using Azure.AI.OpenAI;
using CorporateHRBot.Options;
using CorporateHRBot.Services;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace CorporateHRBot.Tests.Services;

public sealed class ModelEvaluatorToolServiceTests
{
    [TestCase(0, "gpt-4.1-nano")]
    [TestCase(1, "gpt-5-mini")]
    [TestCase(2, "claude-3-5-sonnet")]
    public async Task GetChatClientByComplexity_WhenInputIsSimple_ReturnsLowComplexityClient(int complexity, string expectedModel)
    {
        // Arrange
        var mockAzureClient = new Mock<AzureOpenAIClient>();
        var mockEvaluateChatClient = new Mock<ChatClient>();
        var mockTargetChatClient = new Mock<ChatClient>();
        var mockOptions = new Mock<IOptions<AIRequestsOptions>>();

        mockAzureClient
            .Setup(c => c.GetChatClient("gpt-4.1-nano"))
            .Returns(mockEvaluateChatClient.Object);

        mockOptions
            .Setup(c => c.Value)
            .Returns(new AIRequestsOptions
            {
                HighComplexityModel = "claude-3-5-sonnet",
                LowComplexityModel = "gpt-4.1-nano",
                BaseEvaluateModel = "gpt-4.1-nano",
                MiddleComplexityModel = "gpt-5-mini",
            });

        ChatCompletion fakeCompletion = ModelReaderWriter.Read<ChatCompletion>(
            BinaryData.FromObjectAsJson(new
            {
                id = "chat-test-id",
                choices = new[]
                {
                    new
                    {
                        finish_reason = "stop",
                        index = 0,
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                complexity = complexity
                            }),
                            role = "assistant"
                        }
                    }
                },
                created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                model = expectedModel
            })
        )!;

        mockEvaluateChatClient
            .Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(fakeCompletion, Mock.Of<PipelineResponse>()));

        var service = new ModelEvaluatorToolService(mockAzureClient.Object, mockOptions.Object);

        // Act
        var resultClient = await service.GetChatClientByComplexity("Hello, how are you?", CancellationToken.None);

        // Assert
        mockAzureClient.Verify(x => x.GetChatClient(expectedModel), Times.AtLeastOnce);
    }
}

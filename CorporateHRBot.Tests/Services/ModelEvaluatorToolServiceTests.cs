using Azure.AI.OpenAI;
using CorporateHRBot.Services;
using Moq;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace CorporateHRBot.Tests.Services;

public sealed class ModelEvaluatorToolServiceTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task GetChatClientByComplexity_WhenInputIsSimple_ReturnsLowComplexityClient(int complexity)
    {
        // Arrange
        var mockAzureClient = new Mock<AzureOpenAIClient>();
        var mockEvaluateChatClient = new Mock<ChatClient>();
        var mockTargetChatClient = new Mock<ChatClient>();

        mockAzureClient
            .Setup(c => c.GetChatClient("gpt-4o-mini"))
            .Returns(mockEvaluateChatClient.Object);

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
                            content = "{\"complexity\": 0}",
                            role = "assistant"
                        }
                    }
                },
                created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                model = "gpt-4o-mini"
            })
        )!;

        mockEvaluateChatClient
            .Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClientResult.FromValue(fakeCompletion, Mock.Of<PipelineResponse>()));

        var service = new ModelEvaluatorToolService(mockAzureClient.Object);

        // Act
        var resultClient = await service.GetChatClientByComplexity("Hello, how are you?", CancellationToken.None);

        // Assert
        Assert.NotNull(resultClient);
    }
}

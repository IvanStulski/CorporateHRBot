using Azure.AI.OpenAI;
using CorporateHRBot.Entities;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
using OpenAI.Chat;

namespace CorporateHRBot.Services;

public interface IHrRagService
{
    public Task SplitDocumentbyChuncksAsync(string fileName, SplitDocumentIntoChunksRequest request);

    public Task<ChatResponse> GetAnswerAsync(string userQuery);
}

public sealed class HrRagService : IHrRagService
{
    private const int NumberOfRelevantChunks = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentsRepository _repository;
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly IModelEvaluatorToolService _modelEvaluatorToolService;

    public HrRagService(
        IDocumentsRepository repository,
        AzureOpenAIClient azureOpenAIClient,
        IUnitOfWork unitOfWork,
        IModelEvaluatorToolService modelEvaluatorToolService)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _azureOpenAIClient = azureOpenAIClient;
        _modelEvaluatorToolService = modelEvaluatorToolService;
    }

    public async Task SplitDocumentbyChuncksAsync(string fileName, SplitDocumentIntoChunksRequest request)
    {
        await _repository.DeleteDocumentChunksByFileName(fileName);

        var chunks = SplitIntoChunks(request.FullText);

        var dbChunks = new List<DocumentChunkEntity>();
        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient("text-embedding-3-small");

        for (int i = 0; i < chunks.Count; i++)
        {
            var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(chunks[i]);
            var vectorValues = embeddingResult.Value.ToFloats().ToArray();
            dbChunks.Add(new DocumentChunkEntity
            {
                FileName = fileName,
                ChunkIndex = i,
                Content = chunks[i],
                Embedding = vectorValues
            });
        }

        await _repository.AddChunksAsync(dbChunks);
        await _unitOfWork.CompeteAsync();
    }

    public async Task<ChatResponse> GetAnswerAsync(string userQuery)
    {
        var relevantChunks = await SearchRelevantChunksAsync(userQuery, topK: NumberOfRelevantChunks);

        string contextText = relevantChunks.Count > 0
            ? string.Join("\n---\n", relevantChunks.Select(c => $"[File: {c.FileName}, Part: {c.ChunkIndex}]\n{c.Content}"))
            : "No information found in corporate documents.";

        var systemPrompt =
            "А сейчас ты — корпоративный HR-ассистент. Отвечай на вопросы сотрудников строго и исключительно на основе предоставленных ниже фрагментов документов компании.\n" +
            "Если в предоставленных документах нет ответа на вопрос, честно скажи: «В имеющихся у меня регламентах компании информации по этому вопросу нет».\n" +
            "Не выдумывай факты и не используй внешнюю информацию.\n\n" +
            $"Контекст из документов:\n{contextText}";

        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userQuery)
        ];

        var chatClient = await _modelEvaluatorToolService.GetChatClientByComplexity(userQuery, CancellationToken.None);
        var completion = await chatClient.CompleteChatAsync(messages);
        var answerText = completion.Value.Content[0].Text;

        var sources = relevantChunks.Select(c => new SourceReference(c.FileName, c.ChunkIndex));

        return new ChatResponse(answerText, sources);
    }

    private static List<string> SplitIntoChunks(string text)
    {
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        foreach (var p in paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(p))
            {
                chunks.Add(p.Trim());
            }
        }

        return chunks;
    }

    private async Task<List<DocumentChunkEntity>> SearchRelevantChunksAsync(string query, int topK)
    {
        var embeddings = await GetQueryEmbeddings(query);

        return await _repository.GetChunksByQuery(query, topK, embeddings);
    }

    private async Task<float[]> GetQueryEmbeddings(string query)
    {
        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient("text-embedding-3-small");
        var queryEmbedding = await embeddingClient.GenerateEmbeddingAsync(query);

        return queryEmbedding.Value.ToFloats().ToArray();
    }
}

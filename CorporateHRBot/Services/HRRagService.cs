using Azure.AI.OpenAI;
using CorporateHRBot.Entities;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
using CorporateHRBot.Options;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CorporateHRBot.Services;

public interface IHrRagService
{
    public Task SplitDocumentByChunksAsync(string fileName, SplitDocumentIntoChunksRequest request);

    public Task<ChatResponse> GetAnswerAsync(string userQuery);
}

public sealed class HrRagService : IHrRagService
{
    private const string NoInformationFoundMessage = "No information found in corporate documents.";
    private const int NumberOfRelevantChunks = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentsRepository _repository;
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly IModelEvaluatorToolService _modelEvaluatorToolService;
    private readonly IOptions<AIRequestsOptions> _azureOpenAIOptions;

    public HrRagService(
        IDocumentsRepository repository,
        AzureOpenAIClient azureOpenAIClient,
        IUnitOfWork unitOfWork,
        IModelEvaluatorToolService modelEvaluatorToolService,
        IOptions<AIRequestsOptions> azureOpenAIOptions)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _azureOpenAIClient = azureOpenAIClient;
        _modelEvaluatorToolService = modelEvaluatorToolService;
        _azureOpenAIOptions = azureOpenAIOptions;
    }

    public async Task SplitDocumentByChunksAsync(string fileName, SplitDocumentIntoChunksRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullText))
        {
            throw new InvalidOperationException("Document text is empty.");
        }

        var chunks = SplitIntoChunks(request.FullText);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "The document does not contain valid text chunks.");
        }

        var dbChunks = new List<DocumentChunkEntity>();
        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAIOptions.Value.EmbeddingModel);
        var embeddingResults = await embeddingClient.GenerateEmbeddingsAsync(chunks);

        if (embeddingResults.Value.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count mismatch. " +
                $"Chunks: {chunks.Count}; " +
                $"Embeddings: {embeddingResults.Value.Count}.");
        }

        for (int i = 0; i < embeddingResults.Value.Count; i++)
        {
            var vectorValues = embeddingResults.Value[i].ToFloats().ToArray();
            dbChunks.Add(new DocumentChunkEntity
            {
                FileName = fileName,
                ChunkIndex = i,
                Content = chunks[i],
                Embedding = vectorValues
            });
        }

        await _unitOfWork.CompeteInTransactionAsync(async () =>
        {
            await _repository.DeleteDocumentChunksByFileNameAsync(fileName);
            await _repository.AddChunksAsync(dbChunks);
        });
    }

    public async Task<ChatResponse> GetAnswerAsync(string userQuery)
    {
        var relevantChunks = await SearchRelevantChunksAsync(userQuery, topK: NumberOfRelevantChunks);

        if (relevantChunks.Count == 0)
        {
            return new ChatResponse(NoInformationFoundMessage, []);
        }

        string contextText = string.Join("\n---\n", relevantChunks.Select(c => $"[File: {c.FileName}, Part: {c.ChunkIndex}]\n{c.Content}"));

        var systemPrompt = $"{_azureOpenAIOptions.Value.QuestionPromptTemplate}\nDocument context:\n{contextText}";

        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userQuery)
        ];

        var chatClient = await _modelEvaluatorToolService.GetChatClientByComplexity(userQuery, default);
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

        return await _repository.GetChunksByQueryAsync(query, topK, embeddings);
    }

    private async Task<float[]> GetQueryEmbeddings(string query)
    {
        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAIOptions.Value.EmbeddingModel);
        var queryEmbedding = await embeddingClient.GenerateEmbeddingAsync(query);

        return queryEmbedding.Value.ToFloats().ToArray();
    }
}

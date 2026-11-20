using CorporateHRBot.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateHRBot.Infrastructure.Repositories;

public interface IDocumentsRepository
{
    Task DeleteDocumentChunksByFileNameAsync(string fileName);
    Task<List<DocumentChunkEntity>> GetDocumentChunksByFileNameAsync(string fileName);
    Task<List<DocumentChunkEntity>> GetChunksByQueryAsync(string query, int top, float[] embeddings);
    Task AddChunksAsync(IEnumerable<DocumentChunkEntity> chunk);
}

public sealed class DocumentsRepository : IDocumentsRepository
{
    private readonly HrRagDbContext _dbContext;
    
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "и", "в", "во", "на", "с", "со", "по", "к", "ко",
        "о", "об", "от", "до", "за", "из", "у", "а", "но",
        "или", "не", "ли", "что", "как", "если", "это",
        "мне", "мой", "моя", "мои", "для", "про", "при",
        "есть", "будет", "быть", "можно", "нужно", "хочу",
        "какой", "какая", "какие", "сколько" 
    };

    public DocumentsRepository(HrRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddChunksAsync(IEnumerable<DocumentChunkEntity> chunks)
    {
        await _dbContext.DocumentChunks.AddRangeAsync(chunks);
    }

    public async Task DeleteDocumentChunksByFileNameAsync(string fileName)
    {
        await _dbContext.DocumentChunks
            .Where(x => x.FileName == fileName)
            .ExecuteDeleteAsync();
    }

    public async Task<List<DocumentChunkEntity>> GetDocumentChunksByFileNameAsync(string fileName)
    {
        return await _dbContext.DocumentChunks.AsNoTracking()
            .Where(x => x.FileName == fileName)
            .ToListAsync();
    }

    public async Task<List<DocumentChunkEntity>> GetChunksByQueryAsync(string query, int topK, float[] queryEmbeddings)
    {
        var allChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .ToListAsync();
        var keywords = ExtractKeywords(query);

        var rankedChunks = allChunks
            .Select(chunk =>
            {
                float vectorScore = CosineSimilarity(chunk.Embedding, queryEmbeddings);
                float keywordScore = CalculateKeywordScore(chunk, keywords);
                float hybridScore = vectorScore * 0.7f + keywordScore * 0.3f;

                return new
                {
                    Chunk = chunk,
                    VectorScore = vectorScore,
                    KeywordScore = keywordScore,
                    Score = hybridScore
                };
            })
            .Where(x => x.VectorScore > 0.25f || x.KeywordScore > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        return rankedChunks;
    }

    private static float CalculateKeywordScore(DocumentChunkEntity chunk, IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return 0;
        }

        string content = chunk.Content.ToLowerInvariant();
        string fileName = chunk.FileName.ToLowerInvariant();

        var totalScore = 0f;

        foreach (string keyword in keywords)
        {
            bool existsInContent = content.Contains(
                keyword,
                StringComparison.OrdinalIgnoreCase);

            bool existsInFileName = fileName.Contains(
                keyword,
                StringComparison.OrdinalIgnoreCase);

            if (existsInContent)
            {
                totalScore += 1.0f;
            }

            if (existsInFileName)
            {
                totalScore += 1.5f;
            }
        }

        float maxPossibleScore = keywords.Count * 2.5f;

        return maxPossibleScore == 0
            ? 0
            : MathF.Min(totalScore / maxPossibleScore, 1.0f);
    }

    private static float CosineSimilarity(float[] vecA, float[] vecB)
    {
        if (vecA.Length != vecB.Length || vecA.Length == 0) return 0;

        float dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < vecA.Length; i++)
        {
            dotProduct += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }
        return (normA == 0 || normB == 0) ? 0 : dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private static string[] ExtractKeywords(string query)
    {
        return query
            .ToLowerInvariant()
            .Split(
                [' ', ',', '.', '!', '?', ':', ';', '"', '\'', '(', ')', '[', ']', '-', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim())
            .Where(word => word.Length >= 3)
            .Where(word => !StopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
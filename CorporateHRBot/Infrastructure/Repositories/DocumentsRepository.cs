using CorporateHRBot.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateHRBot.Infrastructure.Repositories;

public interface IDocumentsRepository
{
    Task DeleteDocumentChunksByFileName(string fileName);
    Task<List<DocumentChunkEntity>> GetDocumentChunksByFileName(string fileName);
    Task<List<DocumentChunkEntity>> GetChunksByQuery(string query, int top, float[] embeddings);
    Task AddChunksAsync(IEnumerable<DocumentChunkEntity> chunk);
}

public sealed class DocumentsRepository : IDocumentsRepository
{
    private readonly HrRagDbContext _dbContext;

    public DocumentsRepository(HrRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task DeleteDocumentChunksByFileName(string fileName)
    {
        await _dbContext.DocumentChunks
            .Where(x => x.FileName == fileName)
            .ExecuteDeleteAsync();
    }

    public async Task<List<DocumentChunkEntity>> GetDocumentChunksByFileName(string fileName)
    {
        return await _dbContext.DocumentChunks.AsNoTracking()
            .Where(x => x.FileName == fileName)
            .ToListAsync();
    }

    public async Task<List<DocumentChunkEntity>> GetChunksByQuery(string query, int topK, float[] queryEmbeddings)
    {
        var allChunks = await _dbContext.DocumentChunks.AsNoTracking().ToListAsync();

        var rankedChunks = allChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CosineSimilarity(chunk.Embedding, queryEmbeddings)
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Chunk)
            .Take(topK)
            .ToList();

        return rankedChunks.ToList();
    }

    public async Task AddChunksAsync(IEnumerable<DocumentChunkEntity> chunks)
    {
        await _dbContext.DocumentChunks.AddRangeAsync(chunks);
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
}
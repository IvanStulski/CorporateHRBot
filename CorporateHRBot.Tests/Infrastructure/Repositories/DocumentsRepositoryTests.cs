using CorporateHRBot.Entities;
using CorporateHRBot.Infrastructure;
using CorporateHRBot.Infrastructure.Repositories;
using CorporateHRBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CorporateHRBot.Tests.Infrastructure.Repositories;

[TestFixture]
public sealed class DocumentsRepositoryTests
{
    private SqliteTestDatabase _database = null!;
    private HrRagDbContext _dbContext = null!;
    private DocumentsRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _database = new SqliteTestDatabase();
        _dbContext = _database.DbContext;

        await _dbContext.DocumentChunks.ExecuteDeleteAsync();

        _repository = new DocumentsRepository(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _database.DisposeAsync();
    }

    [Test]
    public async Task AddChunksAsync_ShouldAddChunksAfterSaveChanges()
    {
        // Arrange
        var chunks = new List<DocumentChunkEntity>
        {
            CreateChunk(
                fileName: "vacation-policy.txt",
                chunkIndex: 0,
                content: "Annual paid leave is 28 calendar days.",
                embedding: [1f, 0f, 0f]),

            CreateChunk(
                fileName: "vacation-policy.txt",
                chunkIndex: 1,
                content: "Additional leave for irregular working hours is 5 days.",
                embedding: [0.9f, 0.1f, 0f])
        };

        // Act
        await _repository.AddChunksAsync(chunks);
        await _dbContext.SaveChangesAsync();

        // Assert
        List<DocumentChunkEntity> savedChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .OrderBy(x => x.ChunkIndex)
            .ToListAsync();

        Assert.That(savedChunks, Has.Count.EqualTo(2));

        Assert.Multiple(() =>
        {
            Assert.That(savedChunks[0].FileName, Is.EqualTo("vacation-policy.txt"));
            Assert.That(savedChunks[0].ChunkIndex, Is.EqualTo(0));
            Assert.That(savedChunks[0].Content, Does.Contain("28 calendar days"));

            Assert.That(savedChunks[1].ChunkIndex, Is.EqualTo(1));
            Assert.That(savedChunks[1].Content, Does.Contain("5 days"));
        });
    }

    [Test]
    public async Task GetDocumentChunksByFileNameAsync_ShouldReturnOnlyRequestedDocumentChunks()
    {
        // Arrange
        _dbContext.DocumentChunks.AddRange(
            CreateChunk(
                "vacation-policy.txt",
                0,
                "Vacation is 28 calendar days.",
                [1f, 0f, 0f]),

            CreateChunk(
                "vacation-policy.txt",
                1,
                "Additional leave is 5 calendar days.",
                [0.9f, 0.1f, 0f]),

            CreateChunk(
                "remote-work-policy.txt",
                0,
                "Employees may work remotely two days per week.",
                [0f, 1f, 0f]));

        await _dbContext.SaveChangesAsync();

        // Act
        List<DocumentChunkEntity> result =
            await _repository.GetDocumentChunksByFileNameAsync(
                "vacation-policy.txt");

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(
            result.All(x => x.FileName == "vacation-policy.txt"),
            Is.True);
    }

    [Test]
    public async Task GetDocumentChunksByFileNameAsync_WhenFileDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        _dbContext.DocumentChunks.Add(
            CreateChunk(
                "existing-policy.txt",
                0,
                "Existing content.",
                [1f, 0f, 0f]));

        await _dbContext.SaveChangesAsync();

        // Act
        List<DocumentChunkEntity> result =
            await _repository.GetDocumentChunksByFileNameAsync(
                "missing-policy.txt");

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task DeleteDocumentChunksByFileNameAsync_ShouldDeleteOnlySelectedDocument()
    {
        // Arrange
        _dbContext.DocumentChunks.AddRange(
            CreateChunk(
                "remove-me.txt",
                0,
                "This chunk must be deleted.",
                [1f, 0f, 0f]),

            CreateChunk(
                "remove-me.txt",
                1,
                "This chunk must be deleted too.",
                [0.9f, 0.1f, 0f]),

            CreateChunk(
                "keep-me.txt",
                0,
                "This chunk must remain.",
                [0f, 1f, 0f]));

        await _dbContext.SaveChangesAsync();

        // Act
        await _repository.DeleteDocumentChunksByFileNameAsync(
            "remove-me.txt");

        // Assert
        List<DocumentChunkEntity> remaining = await _dbContext.DocumentChunks
            .AsNoTracking()
            .ToListAsync();

        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].FileName, Is.EqualTo("keep-me.txt"));
    }

    [Test]
    public async Task GetChunksByQueryAsync_ShouldReturnMostRelevantChunkFirst()
    {
        // Arrange
        _dbContext.DocumentChunks.AddRange(
            CreateChunk(
                "vacation-policy.txt",
                0,
                "Annual vacation leave is 28 calendar days.",
                [1f, 0f, 0f]),

            CreateChunk(
                "remote-work-policy.txt",
                0,
                "Employees can work from home two days per week.",
                [0f, 1f, 0f]),

            CreateChunk(
                "medical-insurance.txt",
                0,
                "Medical insurance includes basic dental treatment.",
                [0f, 0f, 1f]));

        await _dbContext.SaveChangesAsync();

        float[] queryEmbedding = [0.99f, 0.01f, 0f];

        // Act
        List<DocumentChunkEntity> result =
            await _repository.GetChunksByQueryAsync(
                "annual vacation leave",
                topK: 2,
                queryEmbeddings: queryEmbedding);

        // Assert
        Assert.That(result, Is.Not.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].FileName, Is.EqualTo("vacation-policy.txt"));
            Assert.That(result[0].Content, Does.Contain("28 calendar days"));
        });
    }

    [Test]
    public async Task GetChunksByQueryAsync_ShouldRespectTopK()
    {
        // Arrange
        _dbContext.DocumentChunks.AddRange(
            CreateChunk(
                "policy-1.txt",
                0,
                "Vacation leave policy.",
                [1f, 0f, 0f]),

            CreateChunk(
                "policy-2.txt",
                0,
                "Vacation leave policy.",
                [0.95f, 0.05f, 0f]),

            CreateChunk(
                "policy-3.txt",
                0,
                "Vacation leave policy.",
                [0.90f, 0.10f, 0f]),

            CreateChunk(
                "policy-4.txt",
                0,
                "Vacation leave policy.",
                [0.85f, 0.15f, 0f]));

        await _dbContext.SaveChangesAsync();

        // Act
        List<DocumentChunkEntity> result =
            await _repository.GetChunksByQueryAsync(
                "vacation leave",
                topK: 2,
                queryEmbeddings: [1f, 0f, 0f]);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetChunksByQueryAsync_WhenNoChunksExist_ShouldReturnEmptyList()
    {
        // Arrange
        float[] queryEmbedding = [1f, 0f, 0f];

        // Act
        List<DocumentChunkEntity> result =
            await _repository.GetChunksByQueryAsync(
                "vacation leave",
                topK: 3,
                queryEmbeddings: queryEmbedding);

        // Assert
        Assert.That(result, Is.Empty);
    }

    private static DocumentChunkEntity CreateChunk(
        string fileName,
        int chunkIndex,
        string content,
        float[] embedding)
    {
        return new DocumentChunkEntity
        {
            FileName = fileName,
            ChunkIndex = chunkIndex,
            Content = content,
            Embedding = embedding
        };
    }
}
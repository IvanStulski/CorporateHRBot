using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CorporateHRBot.Entities;

public class DocumentChunkEntity
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = "";

    public string EmbeddingJson { get; set; } = "[]";

    // Удобное свойство для работы с массивом чисел в C# (не сохраняется в БД напрямую)
    [NotMapped]
    public float[] Embedding
    {
        get => string.IsNullOrEmpty(EmbeddingJson) ? [] : JsonSerializer.Deserialize<float[]>(EmbeddingJson)!;
        set => EmbeddingJson = JsonSerializer.Serialize(value);
    }
}

using CorporateHRBot.Entities;

namespace CorporateHRBot.Services;

public interface IDocumentService
{
    Task InsertFile(IFormFile file, CancellationToken cancellationToken);
}

public sealed class DocumentService : IDocumentService
{
    private readonly IHrRagService _hrRagService;

    public DocumentService(IHrRagService hrRagService)
    {
        _hrRagService = hrRagService;
    }

    public async Task InsertFile(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var readStream = new StreamReader(stream);

        var content = await readStream.ReadToEndAsync(cancellationToken);

        await _hrRagService.SplitDocumentbyChuncksAsync(file.FileName, new SplitDocumentIntoChunksRequest(content));
    }
}

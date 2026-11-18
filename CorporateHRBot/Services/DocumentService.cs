using CorporateHRBot.Infrastructure.Repositories;

namespace CorporateHRBot.Services;

public interface IDocumentService
{
    Task InsertFile(IFormFile file);
}

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentsRepository _repository;

    public DocumentService(IDocumentsRepository repository)
    {
        _repository = repository;
    }

    public async Task InsertFile(IFormFile file)
    {
        //file.
    }
}

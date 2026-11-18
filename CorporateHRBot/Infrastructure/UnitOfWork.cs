namespace CorporateHRBot.Infrastructure;

public interface IUnitOfWork
{
    Task CompeteAsync(CancellationToken cancellation = default);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly HrRagDbContext _dbContext;

    public UnitOfWork(HrRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CompeteAsync(CancellationToken cancellation = default)
    {
        await _dbContext.SaveChangesAsync(cancellation);
    }
}

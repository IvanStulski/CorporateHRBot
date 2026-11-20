namespace CorporateHRBot.Infrastructure;

public interface IUnitOfWork
{
    Task CompeteInTransactionAsync(Func<Task> action, CancellationToken cancellation = default);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly HrRagDbContext _dbContext;

    public UnitOfWork(HrRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CompeteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            await action();

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

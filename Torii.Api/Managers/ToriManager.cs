using Torii.Api.Data;

namespace Torii.Api.Managers;

public class ToriManager(ToriiDbContext db)
{
    protected readonly ToriiDbContext _dbContext = db;
}
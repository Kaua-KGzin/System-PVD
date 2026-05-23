using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Archlab.Backend.Data;

// Used only by EF Core tools (dotnet ef migrations add/update).
// Points to the local docker-compose PostgreSQL so migrations are generated
// with correct PostgreSQL types instead of SQLite types.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PdvDbContext>
{
    public PdvDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ARCHLAB_DESIGN_CONN")
            ?? "Host=localhost;Port=5433;Database=archlab;Username=archlab;Password=archlab_dev_password";

        var options = new DbContextOptionsBuilder<PdvDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PdvDbContext(options);
    }
}

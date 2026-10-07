using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SyteQuery.Features.Database.Data;

/// <summary>
/// Lets `dotnet ef migrations add` work directly against SyteQuery.Core, which - being a
/// class library with no Program.cs/host of its own - has nothing for the EF tooling to
/// introspect otherwise. The connection string here only matters at design time (schema
/// generation); the real app supplies its own via ServiceCollectionExtensions.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_time_only.db");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

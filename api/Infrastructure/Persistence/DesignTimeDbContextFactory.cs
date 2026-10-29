using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpsPilotDbContext>
{
    public OpsPilotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("OPSPILOT_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=55432;Database=opspilot;Username=opspilot;Password=opspilot_dev";

        var options = new DbContextOptionsBuilder<OpsPilotDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OpsPilotDbContext(options);
    }
}
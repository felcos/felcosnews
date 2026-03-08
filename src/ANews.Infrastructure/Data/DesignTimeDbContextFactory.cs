using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ANews.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=anews_dev;Username=anews;Password=dev_password",
            o => o.MigrationsAssembly("ANews.Infrastructure"));

        return new AppDbContext(optionsBuilder.Options);
    }
}

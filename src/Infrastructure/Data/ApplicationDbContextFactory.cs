using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EbayClone.Infrastructure.Data;

public class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
       .SetBasePath(Path.Combine(
           Directory.GetCurrentDirectory(), "..", "Web"))
       .AddJsonFile("appsettings.json", optional: false)
       .AddJsonFile("appsettings.Development.json", optional: true)
       .AddEnvironmentVariables()
       .Build();


        var connectionString =
            configuration.GetConnectionString("MyCnn");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}

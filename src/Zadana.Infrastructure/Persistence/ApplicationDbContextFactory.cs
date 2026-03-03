using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // For design-time tools, we can load configuration from the API project
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Zadana.Api");
        
        // Ensure the path exists, if not fallback to current directory
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=ZadanaDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        });

        // We can pass null or a dummy interceptor for design-time since it's just building the model
        return new ApplicationDbContext(optionsBuilder.Options, new AuditableEntityInterceptor());
    }
}

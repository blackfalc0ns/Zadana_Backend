using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.UnitTests.Common;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    public static SqliteTestDatabase CreateSqlite()
    {
        var connectionString = $"Data Source=zadanatest-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options, new AuditableEntityInterceptor()))
        {
            context.Database.EnsureCreated();
        }

        return new SqliteTestDatabase(connection);
    }
}

public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _rootConnection;
    private readonly string _connectionString;

    public SqliteTestDatabase(SqliteConnection connection)
    {
        _rootConnection = connection;
        _connectionString = connection.ConnectionString;
    }

    public ApplicationDbContext CreateContext()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    public void Dispose()
    {
        _rootConnection.Dispose();
    }
}

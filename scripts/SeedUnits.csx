#r "nuget: Microsoft.Data.SqlClient, 6.0.1"

using Microsoft.Data.SqlClient;

var scriptPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "seed_units_of_measure.sql"));
var connectionString = Environment.GetEnvironmentVariable("ZADANA_SEED_CONNECTION_STRING");

if (Args.Count > 0 && !string.IsNullOrWhiteSpace(Args[0]))
{
    connectionString = Args[0];
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Missing connection string. Pass it as the first argument or set ZADANA_SEED_CONNECTION_STRING.");
}

if (!File.Exists(scriptPath))
{
    throw new FileNotFoundException("SQL seed script was not found.", scriptPath);
}

var sql = await File.ReadAllTextAsync(scriptPath);

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

foreach (var batch in SplitSqlBatches(sql))
{
    if (string.IsNullOrWhiteSpace(batch))
    {
        continue;
    }

    await using var command = connection.CreateCommand();
    command.CommandText = batch;
    command.CommandTimeout = 60;
    await command.ExecuteNonQueryAsync();
}

Console.WriteLine("Units of measure seed completed.");

static IEnumerable<string> SplitSqlBatches(string sql)
{
    using var reader = new StringReader(sql);
    var batch = new List<string>();

    while (reader.ReadLine() is { } line)
    {
        if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
        {
            yield return string.Join(Environment.NewLine, batch);
            batch.Clear();
            continue;
        }

        batch.Add(line);
    }

    if (batch.Count > 0)
    {
        yield return string.Join(Environment.NewLine, batch);
    }
}

using Microsoft.Data.SqlClient;

var scriptPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "seed_units_of_measure.sql"));

var connectionString = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1]
    : Environment.GetEnvironmentVariable("ZADANA_SEED_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Missing connection string. Pass it as the second argument or set ZADANA_SEED_CONNECTION_STRING.");
    return 1;
}

if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"SQL seed script was not found: {scriptPath}");
    return 1;
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
return 0;

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

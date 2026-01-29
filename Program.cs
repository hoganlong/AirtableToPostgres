using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using AirtableToPostgres;

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Check if running in query mode
        if (args.Length > 0 && args[0] == "query")
        {
            await QueryRunner.Run();
            return;
        }

        // Check if running in test mode
        if (args.Length > 0 && args[0] == "test")
        {
            await QuickTest.Run();
            return;
        }

        // Check if running in insights mode
        if (args.Length > 0 && args[0] == "insights")
        {
            await ShowInsights.Run();
            return;
        }

        // Check if running in showall mode
        if (args.Length > 0 && args[0] == "showall")
        {
            await ShowAll.Run();
            return;
        }

        var airtableApiKey = configuration["Airtable:ApiKey"];
        var airtableBaseId = configuration["Airtable:BaseId"];
        var airtableTableName = configuration["Airtable:TableName"];
        var postgresConnectionString = configuration["PostgreSQL:ConnectionString"];
        var useCustomSchema = configuration.GetValue<bool>("Schema:UseCustomSchema", false);
        var syncAllTables = configuration.GetValue<bool>("Schema:SyncAllTables", false);

        Console.WriteLine("Starting Airtable to PostgreSQL migration...");
        Console.WriteLine($"Mode: {(useCustomSchema ? "Custom Schema (Typed Columns)" : "JSONB (Legacy)")}");

        try
        {
            if (useCustomSchema)
            {
                // NEW: Schema-aware approach
                var schemaFilePath = configuration["Schema:SchemaFilePath"] ?? "airtable_schema.txt";
                var schemaParser = new SchemaParser();
                var airtableSchema = schemaParser.Parse(schemaFilePath);
                Console.WriteLine($"Parsed schema file: {airtableSchema.Tables.Count} tables found");

                await using var connection = new NpgsqlConnection(postgresConnectionString);
                await connection.OpenAsync();
                Console.WriteLine("Connected to PostgreSQL\n");

                if (syncAllTables)
                {
                    // Sync all tables
                    Console.WriteLine($"Syncing all {airtableSchema.Tables.Count} tables...\n");
                    var totalRecords = 0;

                    for (int i = 0; i < airtableSchema.Tables.Count; i++)
                    {
                        var tableSchema = airtableSchema.Tables[i];
                        Console.WriteLine($"[{i + 1}/{airtableSchema.Tables.Count}] Processing table: {tableSchema.Name}");
                        Console.WriteLine(new string('=', 60));

                        var records = await SyncTable(
                            airtableApiKey,
                            airtableBaseId,
                            tableSchema,
                            connection,
                            configuration);

                        totalRecords += records;
                        Console.WriteLine();
                    }

                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine($"✓ Successfully synced all {airtableSchema.Tables.Count} tables");
                    Console.WriteLine($"✓ Total records synced: {totalRecords}");
                }
                else
                {
                    // Sync single table
                    var tableSchema = airtableSchema.Tables
                        .FirstOrDefault(t => t.Name == airtableTableName);

                    if (tableSchema == null)
                    {
                        Console.WriteLine($"Error: Table '{airtableTableName}' not found in schema file");
                        Console.WriteLine($"Available tables: {string.Join(", ", airtableSchema.Tables.Select(t => t.Name))}");
                        return;
                    }

                    Console.WriteLine($"Syncing single table: {tableSchema.Name}\n");
                    await SyncTable(
                        airtableApiKey,
                        airtableBaseId,
                        tableSchema,
                        connection,
                        configuration);
                }
            }
            else
            {
                // OLD: JSONB approach (existing code)
                var records = await FetchAirtableRecords(airtableApiKey, airtableBaseId, airtableTableName);
                Console.WriteLine($"Fetched {records.Count} records from Airtable");
                await SaveToPostgreSQL(records, postgresConnectionString);
                Console.WriteLine("Successfully saved all records to PostgreSQL");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static async Task<int> SyncTable(
        string apiKey,
        string baseId,
        TableSchema tableSchema,
        NpgsqlConnection connection,
        IConfiguration configuration)
    {
        var records = await FetchAirtableRecords(apiKey, baseId, tableSchema.Name);
        Console.WriteLine($"  Fetched {records.Count} records from Airtable");

        var typeMapper = new TypeMapper(configuration);
        var schemaGenerator = new SchemaGenerator(typeMapper);
        var recordMapper = new RecordMapper(tableSchema, typeMapper);

        var postgresTableName = SchemaGenerator.SanitizeTableName(tableSchema.Name);
        Console.WriteLine($"  PostgreSQL table: {postgresTableName}");
        Console.WriteLine($"  Fields: {tableSchema.Fields.Count}");

        var ddl = schemaGenerator.GenerateCreateTableDdl(tableSchema, postgresTableName);
        await ExecuteDdl(connection, ddl);
        Console.WriteLine($"  ✓ Table schema created/verified");

        await SaveRecordsWithSchema(connection, records, recordMapper, postgresTableName);
        Console.WriteLine($"  ✓ Saved {records.Count} records");

        return records.Count;
    }

    static async Task<List<JObject>> FetchAirtableRecords(string apiKey, string baseId, string tableName)
    {
        var records = new List<JObject>();
        string offset = null;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        do
        {
            var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}";
            if (!string.IsNullOrEmpty(offset))
            {
                url += $"?offset={offset}";
            }

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var recordsArray = json["records"] as JArray;
            if (recordsArray != null)
            {
                foreach (var record in recordsArray)
                {
                    records.Add(record as JObject);
                }
            }

            offset = json["offset"]?.ToString();
        } while (!string.IsNullOrEmpty(offset));

        return records;
    }

    static async Task SaveToPostgreSQL(List<JObject> records, string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tableName = "airtable_records";

        await CreateTableIfNotExists(connection, tableName);

        foreach (var record in records)
        {
            var recordId = record["id"]?.ToString();
            var fields = record["fields"]?.ToString();
            var createdTime = record["createdTime"]?.ToString();

            var sql = $@"
                INSERT INTO {tableName} (airtable_id, fields, created_time, synced_at)
                VALUES (@airtableId, @fields::jsonb, @createdTime, @syncedAt)
                ON CONFLICT (airtable_id)
                DO UPDATE SET
                    fields = EXCLUDED.fields,
                    created_time = EXCLUDED.created_time,
                    synced_at = EXCLUDED.synced_at";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("airtableId", recordId);
            cmd.Parameters.AddWithValue("fields", fields ?? "{}");
            cmd.Parameters.AddWithValue("createdTime", DateTime.Parse(createdTime));
            cmd.Parameters.AddWithValue("syncedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine($"Saved {records.Count} records to PostgreSQL table '{tableName}'");
    }

    static async Task CreateTableIfNotExists(NpgsqlConnection connection, string tableName)
    {
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id SERIAL PRIMARY KEY,
                airtable_id VARCHAR(255) UNIQUE NOT NULL,
                fields JSONB NOT NULL,
                created_time TIMESTAMP NOT NULL,
                synced_at TIMESTAMP NOT NULL
            )";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task ExecuteDdl(NpgsqlConnection connection, string ddl)
    {
        await using var cmd = new NpgsqlCommand(ddl, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task SaveRecordsWithSchema(
        NpgsqlConnection connection,
        List<JObject> records,
        RecordMapper mapper,
        string tableName)
    {
        var recordCount = 0;

        foreach (var record in records)
        {
            var columns = mapper.MapRecordToColumns(record);

            // Build dynamic INSERT with ON CONFLICT
            var columnNames = string.Join(", ", columns.Keys);
            var paramNames = string.Join(", ", columns.Keys.Select(k => $"@{k}"));
            var updateSet = string.Join(", ",
                columns.Keys.Where(k => k != "airtable_id")
                    .Select(k => $"{k} = EXCLUDED.{k}"));

            var sql = $@"
                INSERT INTO {tableName} ({columnNames})
                VALUES ({paramNames})
                ON CONFLICT (airtable_id)
                DO UPDATE SET {updateSet}";

            await using var cmd = new NpgsqlCommand(sql, connection);

            foreach (var kvp in columns)
            {
                var value = kvp.Value ?? DBNull.Value;

                // Check if this is a JSON string for a JSONB column
                if (value is string strValue && mapper.IsJsonbColumn(kvp.Key))
                {
                    var param = cmd.Parameters.Add($"@{kvp.Key}", NpgsqlTypes.NpgsqlDbType.Jsonb);
                    param.Value = strValue;
                }
                else
                {
                    cmd.Parameters.AddWithValue($"@{kvp.Key}", value);
                }
            }

            await cmd.ExecuteNonQueryAsync();
            recordCount++;

            // Only show progress for larger tables (every 100 records)
            if (records.Count > 200 && recordCount % 100 == 0)
            {
                Console.Write($"\r    Progress: {recordCount}/{records.Count} records...");
            }
        }

        // Clear progress line if it was shown
        if (records.Count > 200)
        {
            Console.Write("\r" + new string(' ', 60) + "\r");
        }
    }
}

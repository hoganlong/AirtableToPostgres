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

        // Check if running in HTML generation mode
        if (args.Length > 0 && args[0] == "html")
        {
            await ArtworkHTML.Run();
            return;
        }

        // Check if running in diagnostic mode
        if (args.Length > 1 && args[0] == "diagnostic")
        {
            await DiagnosticFetch.Run(args[1]);
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

        var syncStartTime = DateTime.UtcNow;
        var globalStatistics = new Dictionary<string, SyncStatistics>();

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

                var syncHistoryLogger = new SyncHistoryLogger();
                await syncHistoryLogger.EnsureHistoryTableExists(connection);

                if (syncAllTables)
                {
                    // Sync all tables
                    Console.WriteLine($"Syncing all {airtableSchema.Tables.Count} tables...\n");

                    for (int i = 0; i < airtableSchema.Tables.Count; i++)
                    {
                        var tableSchema = airtableSchema.Tables[i];
                        Console.WriteLine($"[{i + 1}/{airtableSchema.Tables.Count}] Processing table: {tableSchema.Name}");
                        Console.WriteLine(new string('=', 60));

                        var statistics = await SyncTable(
                            airtableApiKey,
                            airtableBaseId,
                            tableSchema,
                            connection,
                            configuration,
                            syncHistoryLogger);

                        globalStatistics[tableSchema.Name] = statistics;
                        Console.WriteLine();
                    }

                    // Calculate totals and log sync history
                    var syncDuration = DateTime.UtcNow - syncStartTime;
                    var historyEntry = new SyncHistoryEntry
                    {
                        SyncTimestamp = syncStartTime,
                        DurationSeconds = (decimal)syncDuration.TotalSeconds,
                        TotalTables = globalStatistics.Count,
                        TotalNew = globalStatistics.Values.Sum(s => s.NewRecords),
                        TotalUpdated = globalStatistics.Values.Sum(s => s.UpdatedRecords),
                        TotalUnchanged = globalStatistics.Values.Sum(s => s.UnchangedRecords),
                        TotalFetched = globalStatistics.Values.Sum(s => s.FetchedRecords),
                        TableDetails = globalStatistics,
                        Status = "success",
                        ErrorMessage = null
                    };

                    await syncHistoryLogger.LogSyncHistory(connection, historyEntry);

                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine("SYNC SUMMARY");
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine($"Tables synced: {historyEntry.TotalTables}");
                    Console.WriteLine($"Records fetched: {historyEntry.TotalFetched}");
                    Console.WriteLine($"NEW records: {historyEntry.TotalNew}");
                    Console.WriteLine($"UPDATED records: {historyEntry.TotalUpdated}");
                    Console.WriteLine($"UNCHANGED records: {historyEntry.TotalUnchanged}");
                    Console.WriteLine($"Total duration: {syncDuration.TotalSeconds:F2}s");
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
                    var statistics = await SyncTable(
                        airtableApiKey,
                        airtableBaseId,
                        tableSchema,
                        connection,
                        configuration,
                        syncHistoryLogger);

                    globalStatistics[tableSchema.Name] = statistics;

                    // Log sync history for single table
                    var syncDuration = DateTime.UtcNow - syncStartTime;
                    var historyEntry = new SyncHistoryEntry
                    {
                        SyncTimestamp = syncStartTime,
                        DurationSeconds = (decimal)syncDuration.TotalSeconds,
                        TotalTables = 1,
                        TotalNew = statistics.NewRecords,
                        TotalUpdated = statistics.UpdatedRecords,
                        TotalUnchanged = statistics.UnchangedRecords,
                        TotalFetched = statistics.FetchedRecords,
                        TableDetails = globalStatistics,
                        Status = "success",
                        ErrorMessage = null
                    };

                    await syncHistoryLogger.LogSyncHistory(connection, historyEntry);
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

            // Log failed sync to history if connection is available
            if (useCustomSchema)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(postgresConnectionString);
                    await connection.OpenAsync();

                    var syncHistoryLogger = new SyncHistoryLogger();
                    await syncHistoryLogger.EnsureHistoryTableExists(connection);

                    var syncDuration = DateTime.UtcNow - syncStartTime;
                    var historyEntry = new SyncHistoryEntry
                    {
                        SyncTimestamp = syncStartTime,
                        DurationSeconds = (decimal)syncDuration.TotalSeconds,
                        TotalTables = globalStatistics.Count,
                        TotalNew = globalStatistics.Values.Sum(s => s.NewRecords),
                        TotalUpdated = globalStatistics.Values.Sum(s => s.UpdatedRecords),
                        TotalUnchanged = globalStatistics.Values.Sum(s => s.UnchangedRecords),
                        TotalFetched = globalStatistics.Values.Sum(s => s.FetchedRecords),
                        TableDetails = globalStatistics,
                        Status = "error",
                        ErrorMessage = ex.Message
                    };

                    await syncHistoryLogger.LogSyncHistory(connection, historyEntry);
                }
                catch
                {
                    // Ignore errors in error logging
                }
            }
        }
    }

    static async Task<SyncStatistics> SyncTable(
        string apiKey,
        string baseId,
        TableSchema tableSchema,
        NpgsqlConnection connection,
        IConfiguration configuration,
        SyncHistoryLogger syncHistoryLogger)
    {
        var tableStartTime = DateTime.UtcNow;
        var statistics = new SyncStatistics { TableName = tableSchema.Name };

        // Get last sync timestamp for incremental sync
        var lastSyncTime = await syncHistoryLogger.GetLastSyncTimestamp(connection, tableSchema.Name);

        if (lastSyncTime.HasValue)
        {
            Console.WriteLine($"  Mode: Incremental sync (since {lastSyncTime.Value:yyyy-MM-dd HH:mm:ss} UTC)");
            Console.WriteLine($"  Filter: IS_AFTER(LAST_MODIFIED_TIME(), \"{lastSyncTime.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}\")");
        }
        else
        {
            Console.WriteLine($"  Mode: Full sync (no history)");
        }

        var records = await FetchAirtableRecords(apiKey, baseId, tableSchema.Name, lastSyncTime);
        statistics.FetchedRecords = records.Count;

        if (lastSyncTime.HasValue)
        {
            Console.WriteLine($"  Fetched {records.Count} records from Airtable (modified since last sync)");
        }
        else
        {
            Console.WriteLine($"  Fetched {records.Count} records from Airtable");
        }

        var typeMapper = new TypeMapper(configuration);
        var schemaGenerator = new SchemaGenerator(typeMapper);
        var recordMapper = new RecordMapper(tableSchema, typeMapper);
        var changeDetector = new ChangeDetector();

        var postgresTableName = SchemaGenerator.SanitizeTableName(tableSchema.Name);
        Console.WriteLine($"  PostgreSQL table: {postgresTableName}");
        Console.WriteLine($"  Fields: {tableSchema.Fields.Count}");

        var ddl = schemaGenerator.GenerateCreateTableDdl(tableSchema, postgresTableName);
        await ExecuteDdl(connection, ddl);
        Console.WriteLine($"  ✓ Table schema created/verified");

        await SaveRecordsWithChangeDetection(
            connection,
            records,
            recordMapper,
            changeDetector,
            postgresTableName,
            statistics);

        // Calculate unchanged records
        var totalRecordsInDb = await changeDetector.GetRecordCount(connection, postgresTableName);
        Console.WriteLine($"  DEBUG: Total records in DB: {totalRecordsInDb}");
        statistics.UnchangedRecords = totalRecordsInDb - statistics.UpdatedRecords - statistics.NewRecords;

        statistics.Duration = DateTime.UtcNow - tableStartTime;

        Console.WriteLine($"  ✓ NEW: {statistics.NewRecords}");
        Console.WriteLine($"  ✓ UPDATED: {statistics.UpdatedRecords}");
        Console.WriteLine($"  ✓ UNCHANGED: {statistics.UnchangedRecords}");
        Console.WriteLine($"  Duration: {statistics.Duration.TotalSeconds:F2}s");

        return statistics;
    }

    static async Task<List<JObject>> FetchAirtableRecords(
        string apiKey,
        string baseId,
        string tableName,
        DateTime? lastSyncTime = null)
    {
        var records = new List<JObject>();
        string? offset = null;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        do
        {
            var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}";

            // Add filter for incremental sync
            if (lastSyncTime.HasValue)
            {
                var isoTime = lastSyncTime.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var formula = $"IS_AFTER(LAST_MODIFIED_TIME(), \"{isoTime}\")";
                url += $"?filterByFormula={Uri.EscapeDataString(formula)}";
            }

            // Add offset for pagination
            if (!string.IsNullOrEmpty(offset))
            {
                url += (lastSyncTime.HasValue ? "&" : "?") + $"offset={offset}";
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

    static async Task SaveRecordsWithChangeDetection(
        NpgsqlConnection connection,
        List<JObject> records,
        RecordMapper mapper,
        ChangeDetector changeDetector,
        string tableName,
        SyncStatistics statistics)
    {
        var recordCount = 0;

        foreach (var record in records)
        {
            var columns = mapper.MapRecordToColumns(record);
            var airtableId = columns["airtable_id"]?.ToString() ?? "";

            // Detect if this is a new or updated record
            var changeStatus = await changeDetector.DetectChange(connection, tableName, airtableId);

            if (changeStatus == ChangeStatus.New)
            {
                await InsertNewRecord(connection, tableName, columns, mapper);
                statistics.IncrementNew();
            }
            else
            {
                columns["last_modified_at"] = DateTime.UtcNow;
                await UpdateExistingRecord(connection, tableName, columns, mapper);
                statistics.IncrementUpdated();
            }

            recordCount++;

            // Show progress for larger tables
            if (records.Count > 200 && recordCount % 100 == 0)
            {
                Console.Write($"\r    Progress: {recordCount}/{records.Count} (N:{statistics.NewRecords} U:{statistics.UpdatedRecords})...");
            }
        }

        // Clear progress line if it was shown
        if (records.Count > 200)
        {
            Console.Write("\r" + new string(' ', 80) + "\r");
        }
    }

    static async Task InsertNewRecord(
        NpgsqlConnection connection,
        string tableName,
        Dictionary<string, object?> columns,
        RecordMapper mapper)
    {
        var columnNames = string.Join(", ", columns.Keys);
        var paramNames = string.Join(", ", columns.Keys.Select(k => $"@{k}"));

        var sql = $"INSERT INTO {tableName} ({columnNames}) VALUES ({paramNames})";

        await using var cmd = new NpgsqlCommand(sql, connection);
        AddParameters(cmd, columns, mapper);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task UpdateExistingRecord(
        NpgsqlConnection connection,
        string tableName,
        Dictionary<string, object?> columns,
        RecordMapper mapper)
    {
        var updateSet = string.Join(", ",
            columns.Keys
                .Where(k => k != "airtable_id" && k != "created_time")
                .Select(k => $"{k} = @{k}"));

        var sql = $"UPDATE {tableName} SET {updateSet} WHERE airtable_id = @airtable_id";

        await using var cmd = new NpgsqlCommand(sql, connection);
        AddParameters(cmd, columns, mapper);
        await cmd.ExecuteNonQueryAsync();
    }

    static void AddParameters(
        NpgsqlCommand cmd,
        Dictionary<string, object?> columns,
        RecordMapper mapper)
    {
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
    }
}

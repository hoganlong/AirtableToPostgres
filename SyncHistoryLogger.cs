using Npgsql;

namespace AirtableToPostgres;

public class SyncHistoryLogger
{
    public async Task EnsureHistoryTableExists(NpgsqlConnection connection)
    {
        // Check if old table structure exists (with table_details JSONB column)
        var checkOldSchemaSql = @"
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'sync_history'
                AND column_name = 'table_details'
            )";

        await using var checkCmd = new NpgsqlCommand(checkOldSchemaSql, connection);
        var hasOldSchema = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

        if (hasOldSchema)
        {
            // Drop old table and recreate with new schema
            Console.WriteLine("  Migrating sync_history table to new schema (one row per table)...");
            var dropSql = "DROP TABLE IF EXISTS sync_history;";
            await using var dropCmd = new NpgsqlCommand(dropSql, connection);
            await dropCmd.ExecuteNonQueryAsync();
        }

        var sql = @"
            CREATE TABLE IF NOT EXISTS sync_history (
                id SERIAL PRIMARY KEY,
                sync_id UUID NOT NULL,
                sync_timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
                table_name TEXT NOT NULL,
                new_records INTEGER NOT NULL,
                updated_records INTEGER NOT NULL,
                unchanged_records INTEGER NOT NULL,
                fetched_records INTEGER NOT NULL,
                duration_seconds NUMERIC(10,2) NOT NULL,
                status TEXT NOT NULL,
                error_message TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_sync_history_timestamp
                ON sync_history(sync_timestamp DESC);

            CREATE INDEX IF NOT EXISTS idx_sync_history_table_name
                ON sync_history(table_name);

            CREATE INDEX IF NOT EXISTS idx_sync_history_sync_id
                ON sync_history(sync_id);

            CREATE INDEX IF NOT EXISTS idx_sync_history_status
                ON sync_history(status);";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DateTime?> GetLastSyncTimestamp(
        NpgsqlConnection connection,
        string tableName)
    {
        var sql = @"
            SELECT sync_timestamp
            FROM sync_history
            WHERE status = 'success'
              AND table_name = @tableName
            ORDER BY sync_timestamp DESC
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("tableName", tableName);

        var result = await cmd.ExecuteScalarAsync();
        return result as DateTime?;
    }

    public async Task LogSyncHistory(
        NpgsqlConnection connection,
        SyncHistoryEntry entry)
    {
        // Insert one row per table
        foreach (var tableDetail in entry.TableDetails)
        {
            var sql = @"
                INSERT INTO sync_history (
                    sync_id,
                    sync_timestamp,
                    table_name,
                    new_records,
                    updated_records,
                    unchanged_records,
                    fetched_records,
                    duration_seconds,
                    status,
                    error_message
                )
                VALUES (
                    @syncId,
                    @syncTimestamp,
                    @tableName,
                    @newRecords,
                    @updatedRecords,
                    @unchangedRecords,
                    @fetchedRecords,
                    @durationSeconds,
                    @status,
                    @errorMessage
                )";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("syncId", entry.SyncId);
            cmd.Parameters.AddWithValue("syncTimestamp", entry.SyncTimestamp);
            cmd.Parameters.AddWithValue("tableName", tableDetail.Key);
            cmd.Parameters.AddWithValue("newRecords", tableDetail.Value.NewRecords);
            cmd.Parameters.AddWithValue("updatedRecords", tableDetail.Value.UpdatedRecords);
            cmd.Parameters.AddWithValue("unchangedRecords", tableDetail.Value.UnchangedRecords);
            cmd.Parameters.AddWithValue("fetchedRecords", tableDetail.Value.FetchedRecords);
            cmd.Parameters.AddWithValue("durationSeconds", (decimal)tableDetail.Value.Duration.TotalSeconds);
            cmd.Parameters.AddWithValue("status", entry.Status);
            cmd.Parameters.AddWithValue("errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}

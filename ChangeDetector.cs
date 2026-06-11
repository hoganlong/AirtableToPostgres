using Npgsql;

namespace AirtableToPostgres;

public class ChangeDetector
{
    public ChangeDetector()
    {
    }

    public async Task<ChangeStatus> DetectChange(
        NpgsqlConnection connection,
        string tableName,
        string airtableId)
    {
        if (string.IsNullOrEmpty(airtableId))
        {
            return ChangeStatus.New;
        }

        var exists = await RecordExists(connection, tableName, airtableId);
        return exists ? ChangeStatus.Updated : ChangeStatus.New;
    }

    private async Task<bool> RecordExists(
        NpgsqlConnection connection,
        string tableName,
        string airtableId)
    {
        var sql = $"SELECT EXISTS(SELECT 1 FROM {tableName} WHERE airtable_id = @airtableId)";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("airtableId", airtableId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && (bool)result;
    }

    public async Task<int> GetRecordCount(
        NpgsqlConnection connection,
        string tableName)
    {
        var sql = $"SELECT COUNT(*) FROM {tableName}";

        await using var cmd = new NpgsqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }
}

public enum ChangeStatus
{
    New,
    Updated
}

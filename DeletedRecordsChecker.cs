using Newtonsoft.Json.Linq;
using Npgsql;

namespace AirtableToPostgres;

public class DeletedRecordsChecker
{
  public async Task Run(
    string apiKey,
    string baseId,
    AirtableSchema schema,
    NpgsqlConnection connection,
    string? singleTable = null)
  {
    var tables = singleTable != null
      ? schema.Tables.Where(t => t.Name.Equals(singleTable, StringComparison.OrdinalIgnoreCase)).ToList()
      : schema.Tables;

    if (tables.Count == 0)
    {
      Console.WriteLine($"Table '{singleTable}' not found in schema.");
      Console.WriteLine($"Available tables: {string.Join(", ", schema.Tables.Select(t => t.Name))}");
      return;
    }

    var totalDeleted = 0;

    Console.WriteLine("Checking for records deleted from Airtable but still in database...\n");

    foreach (var table in tables)
    {
      var pgTable = SchemaGenerator.SanitizeTableName(table.Name);
      Console.WriteLine($"Table: {table.Name} (pg: {pgTable})");

      // Check if the pg table exists
      if (!await TableExists(connection, pgTable))
      {
        Console.WriteLine($"  (table does not exist in database yet — skipping)\n");
        continue;
      }

      Console.Write($"  Fetching all IDs from Airtable...");
      var airtableIds = await FetchAllAirtableIds(apiKey, baseId, table.Name);
      Console.WriteLine($" {airtableIds.Count} records");

      var labelColumn = await FindLabelColumn(connection, pgTable);

      Console.Write($"  Fetching all IDs from database...");
      var dbIds = await FetchAllDbIds(connection, pgTable, labelColumn);
      Console.WriteLine($" {dbIds.Count} records");

      var deletedIds = dbIds.Keys.Except(airtableIds).ToList();
      totalDeleted += deletedIds.Count;

      if (deletedIds.Count == 0)
      {
        Console.WriteLine($"  ✓ No deleted records found\n");
        continue;
      }

      Console.WriteLine($"  ✗ {deletedIds.Count} record(s) in DB but NOT in Airtable:\n");

      foreach (var id in deletedIds)
      {
        dbIds.TryGetValue(id, out var row);
        var syncedAt = row.syncedAt.ToString("yyyy-MM-dd HH:mm");
        var label = row.label != null ? $"  \"{row.label}\"" : "";
        Console.WriteLine($"    {id}{label}  (last synced: {syncedAt})");
      }

      Console.WriteLine();
      Console.Write($"  Generate DELETE SQL for {pgTable}? (y/n): ");
      var answer = Console.ReadLine()?.Trim().ToLower();
      if (answer == "y")
      {
        var backupTable = $"{pgTable}_backup_{DateTime.Now:yyMMdd}";
        var idList = string.Join(", ", deletedIds.Select(id => $"'{id}'"));
        Console.WriteLine();
        Console.WriteLine($"  -- Backup {deletedIds.Count} record(s) then delete from {pgTable}");
        Console.WriteLine($"  CREATE TABLE {backupTable} AS SELECT * FROM {pgTable} WHERE airtable_id IN ({idList});");
        Console.WriteLine($"  DELETE FROM {pgTable} WHERE airtable_id IN ({idList});");
        Console.WriteLine();
      }
    }

    Console.WriteLine(new string('=', 50));
    Console.WriteLine($"TOTAL deleted records found: {totalDeleted}");
  }

  private async Task<HashSet<string>> FetchAllAirtableIds(string apiKey, string baseId, string tableName)
  {
    var ids = new HashSet<string>();
    string? offset = null;

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    do
    {
      // Record id is always returned at the top level; no fields filter needed
      var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}";

      if (!string.IsNullOrEmpty(offset))
        url += $"?offset={offset}";

      var response = await httpClient.GetAsync(url);
      response.EnsureSuccessStatusCode();

      var json = JObject.Parse(await response.Content.ReadAsStringAsync());

      foreach (var record in json["records"] as JArray ?? new JArray())
      {
        var id = record["id"]?.ToString();
        if (id != null) ids.Add(id);
      }

      offset = json["offset"]?.ToString();
    } while (!string.IsNullOrEmpty(offset));

    return ids;
  }

  private async Task<Dictionary<string, (string? label, DateTime syncedAt)>> FetchAllDbIds(
    NpgsqlConnection connection, string tableName, string? labelColumn)
  {
    var result = new Dictionary<string, (string? label, DateTime syncedAt)>();

    var labelSql = labelColumn != null ? $", {labelColumn}::text" : "";
    await using var cmd = new NpgsqlCommand(
      $"SELECT airtable_id, synced_at{labelSql} FROM {tableName}", connection);

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      var id = reader.GetString(0);
      var syncedAt = reader.GetDateTime(1);
      var label = labelColumn != null && !reader.IsDBNull(2) ? reader.GetString(2) : null;
      result[id] = (label, syncedAt);
    }

    return result;
  }

  private async Task<bool> TableExists(NpgsqlConnection connection, string tableName)
  {
    await using var cmd = new NpgsqlCommand(
      "SELECT 1 FROM information_schema.tables WHERE table_name = @t", connection);
    cmd.Parameters.AddWithValue("t", tableName);
    return await cmd.ExecuteScalarAsync() != null;
  }

  private async Task<string?> FindLabelColumn(NpgsqlConnection connection, string tableName)
  {
    // Look for a human-readable column to show alongside the airtable_id
    var candidates = new[] { "title", "name", "ifilename", "filename", "description" };

    await using var cmd = new NpgsqlCommand(
      "SELECT column_name FROM information_schema.columns WHERE table_name = @t", connection);
    cmd.Parameters.AddWithValue("t", tableName);

    var columns = new HashSet<string>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
      columns.Add(reader.GetString(0).ToLower());

    return candidates.FirstOrDefault(c => columns.Contains(c));
  }
}

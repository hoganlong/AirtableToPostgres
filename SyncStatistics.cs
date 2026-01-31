namespace AirtableToPostgres;

public class SyncStatistics
{
    public string TableName { get; set; } = string.Empty;
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int UnchangedRecords { get; set; }
    public int FetchedRecords { get; set; }
    public int TotalRecords => NewRecords + UpdatedRecords + UnchangedRecords;
    public TimeSpan Duration { get; set; }

    public void IncrementNew()
    {
        NewRecords++;
    }

    public void IncrementUpdated()
    {
        UpdatedRecords++;
    }

    public void IncrementUnchanged()
    {
        UnchangedRecords++;
    }
}

public class SyncHistoryEntry
{
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public DateTime SyncTimestamp { get; set; }
    public decimal DurationSeconds { get; set; }
    public int TotalTables { get; set; }
    public int TotalNew { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalUnchanged { get; set; }
    public int TotalFetched { get; set; }
    public Dictionary<string, SyncStatistics> TableDetails { get; set; } = new();
    public string Status { get; set; } = "success";
    public string? ErrorMessage { get; set; }
}

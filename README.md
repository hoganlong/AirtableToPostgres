# Airtable to PostgreSQL Migration Tool

A .NET console application that reads data from an Airtable database and saves it to a PostgreSQL database with intelligent incremental sync.

## Features

- **Incremental Sync**: Only fetches changed records using Airtable's `LAST_MODIFIED_TIME()`
- **Smart Change Detection**: Tracks NEW, UPDATED, and UNCHANGED records
- **Sync History**: Logs every sync operation with detailed per-table statistics
- **Performance**: 90-95% faster on subsequent syncs with minimal changes
- Handles pagination automatically
- Creates PostgreSQL tables with typed columns
- Uses upsert logic to avoid duplicates
- Automatic schema migration

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database
- Airtable API key and base information

## Configuration

Update the `appsettings.json` file with your credentials:

```json
{
  "Airtable": {
    "ApiKey": "YOUR_AIRTABLE_API_KEY",
    "BaseId": "YOUR_BASE_ID",
    "TableName": "YOUR_TABLE_NAME"
  },
  "PostgreSQL": {
    "ConnectionString": "Host=localhost;Port=5432;Database=your_database;Username=your_username;Password=your_password"
  }
}
```

### Getting Airtable Credentials

1. **API Key**: Go to https://airtable.com/account and create a personal access token
2. **Base ID**: Found in the Airtable API documentation for your base (looks like `appXXXXXXXXXXXXXX`)
3. **Table Name**: The name of your table in Airtable

## Database Schema

The application creates a table named `airtable_records` with the following structure:

```sql
CREATE TABLE airtable_records (
    id SERIAL PRIMARY KEY,
    airtable_id VARCHAR(255) UNIQUE NOT NULL,
    fields JSONB NOT NULL,
    created_time TIMESTAMP NOT NULL,
    synced_at TIMESTAMP NOT NULL
)
```

## Usage

### Sync Data from Airtable
1. Update `appsettings.json` with your credentials
2. Run the application:

```bash
cd AirtableToPostgres
dotnet run
```

### Generate HTML Gallery
Create a static HTML website to browse your artwork collection:

```bash
dotnet run -- html
```

Opens an `artwork_html` folder with:
- **index.html** - Main page with statistics
- **artworks.html** - Complete artwork list
- **series.html** - View by series
- **locations.html** - View by location

No server needed - just open `index.html` in your browser!

### Other Commands
```bash
dotnet run                          # Incremental sync all tables
dotnet run -- full                  # Force full sync all tables
dotnet run -- sync ARTWORK          # Incremental sync one table
dotnet run -- sync ARTWORK full     # Force full sync one table
dotnet run -- query                 # Interactive query explorer
dotnet run -- showall               # Show all insights
dotnet run -- test                  # Test database connection
dotnet run -- deleted               # Check all tables for records deleted in Airtable
dotnet run -- deleted ARTWORK       # Check a single table for deleted records
```

### Deleted Records Check

The `deleted` mode fetches all record IDs from Airtable and compares them against the database. Since syncs never delete rows, this reports any records present in the DB but no longer in Airtable.

For each table with deleted records, it prints the airtable ID, an optional label (title/name/filename if available), and the last synced timestamp. It then prompts:

```
  Generate DELETE SQL for artwork? (y/n):
```

If you answer `y`, it outputs SQL that first backs up the rows to a timestamped table, then deletes them:

```sql
CREATE TABLE artwork_backup_260327 AS SELECT * FROM artwork WHERE airtable_id IN ('recABC123');
DELETE FROM artwork WHERE airtable_id IN ('recABC123');
```

The backup table name format is `<table>_backup_YYMMDD`. No data is modified by the tool itself — the SQL is printed for you to review and run manually.

## How It Works

1. Reads configuration from `appsettings.json`
2. Fetches all records from Airtable using their REST API
3. Handles pagination to retrieve all records
4. Creates the PostgreSQL table if it doesn't exist
5. Inserts/updates each record using UPSERT logic
6. Stores Airtable fields as JSONB for flexibility

## Error Handling

The application includes basic error handling and will display error messages if:
- Connection to Airtable fails
- PostgreSQL connection fails
- Invalid credentials are provided

## Extending the Application

You can modify the schema in the `CreateTableIfNotExists` method to create a custom table structure that matches your specific Airtable fields instead of using JSONB.

## Documentation

- **[PROJECT_STATUS.md](PROJECT_STATUS.md)** - Complete project status and features
- **[INCREMENTAL_SYNC.md](INCREMENTAL_SYNC.md)** - Detailed incremental sync implementation guide
- **[HTML_GENERATOR.md](HTML_GENERATOR.md)** - HTML gallery generator guide
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Technical implementation details
- **[EXAMPLE_QUERIES.md](EXAMPLE_QUERIES.md)** - 30+ example SQL queries

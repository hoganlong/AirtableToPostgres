# Airtable to PostgreSQL Migration Tool

A .NET console application that reads data from an Airtable database and saves it to a PostgreSQL database.

## Features

- Fetches all records from a specified Airtable table
- Handles pagination automatically
- Creates PostgreSQL table if it doesn't exist
- Stores Airtable records with their fields as JSONB
- Uses upsert logic to avoid duplicates

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

1. Update `appsettings.json` with your credentials
2. Run the application:

```bash
cd AirtableToPostgres
dotnet run
```

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

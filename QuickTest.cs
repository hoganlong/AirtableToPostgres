using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;

public class QuickTest
{
    public static async Task Run()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration["PostgreSQL:ConnectionString"];

        Console.WriteLine("═══ Table Overview ═══\n");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"SELECT 'ARTWORK' as table_name, COUNT(*) as records FROM artwork
                    UNION ALL SELECT 'ARTWORK_IMAGE', COUNT(*) FROM artwork_image
                    UNION ALL SELECT 'PHOTO', COUNT(*) FROM photo
                    UNION ALL SELECT 'SOLD', COUNT(*) FROM sold
                    UNION ALL SELECT 'ARCHIVE', COUNT(*) FROM archive
                    ORDER BY records DESC";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);

        Console.WriteLine($"{"Table Name",-20} {"Records",10}");
        Console.WriteLine(new string('─', 32));

        foreach (DataRow row in table.Rows)
        {
            Console.WriteLine($"{row["table_name"],-20} {row["records"],10:N0}");
        }

        Console.WriteLine($"\n({table.Rows.Count} tables)");
        Console.WriteLine("\n✓ Query tool is working!");
        Console.WriteLine("\nTo explore your data interactively:");
        Console.WriteLine("  Double-click: QueryExplorer.bat");
        Console.WriteLine("  Or run: dotnet run -- query");
    }
}

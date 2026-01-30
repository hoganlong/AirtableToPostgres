using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;

public class ArtworkHTML
{
    private readonly string _connectionString;

    public ArtworkHTML(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task Run()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration["PostgreSQL:ConnectionString"];
        var insights = new ArtworkHTML(connectionString!);

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Keith Long Archive - Artwork HTML Generation         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await insights.ShowRecentArtworks();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

    }

    private async Task ShowArtworks()
    {
        Console.WriteLine("═══ Recent Artworks (Last 10) ═══\n");

        var sql = @"SELECT *
                    FROM artwork
                    LIMIT 10";

        await PrintResults(sql);
    }

    private async Task PrintResults(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);

        if (table.Rows.Count == 0)
        {
            Console.WriteLine("No results found.");
            return;
        }

        // Calculate column widths
        var columnWidths = new int[table.Columns.Count];
        for (int i = 0; i < table.Columns.Count; i++)
        {
            columnWidths[i] = Math.Max(
                table.Columns[i].ColumnName.Length,
                table.Rows.Cast<DataRow>()
                    .Max(row => row[i]?.ToString()?.Length ?? 0)
            );
            columnWidths[i] = Math.Min(columnWidths[i], 50);
        }

        // Print header
        for (int i = 0; i < table.Columns.Count; i++)
        {
            Console.Write(table.Columns[i].ColumnName.PadRight(columnWidths[i] + 2));
        }
        Console.WriteLine();

        // Print separator
        for (int i = 0; i < table.Columns.Count; i++)
        {
            Console.Write(new string('─', columnWidths[i] + 2));
        }
        Console.WriteLine();

        // Print rows
        foreach (DataRow row in table.Rows)
        {
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var value = row[i]?.ToString() ?? "";
                if (value.Length > 50)
                {
                    value = value.Substring(0, 47) + "...";
                }
                Console.Write(value.PadRight(columnWidths[i] + 2));
            }
            Console.WriteLine();
        }

        Console.WriteLine($"\n({table.Rows.Count} row{(table.Rows.Count != 1 ? "s" : "")})");
    }
}

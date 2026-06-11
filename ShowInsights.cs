using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;

public class ShowInsights
{
    private readonly string _connectionString;

    public ShowInsights(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task Run(string inConStr)
    {
        var insights = new ShowInsights(inConStr!);

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Keith Long Archive - Data Insights              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await insights.ShowRecentArtworks();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

        await insights.ShowArtworksByLocation();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

        await insights.ShowArtworksBySeries();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

        await insights.ShowSalesSummary();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

        await insights.ShowRecentSales();
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.WriteLine();

        await insights.ShowPhotographerStats();
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private async Task ShowRecentArtworks()
    {
        Console.WriteLine("═══ Recent Artworks (Last 10) ═══\n");

        var sql = @"SELECT id_field as id, title, series,
                           TO_CHAR(create_dt, 'YYYY-MM-DD') as created,
                           medium, location
                    FROM artwork
                    WHERE create_dt IS NOT NULL
                    ORDER BY create_dt DESC
                    LIMIT 10";

        await PrintResults(sql);
    }

    private async Task ShowArtworksByLocation()
    {
        Console.WriteLine("═══ Artworks by Location ═══\n");

        var sql = @"SELECT location, COUNT(*) as count
                    FROM artwork
                    WHERE location IS NOT NULL
                    GROUP BY location
                    ORDER BY count DESC";

        await PrintResults(sql);
    }

    private async Task ShowArtworksBySeries()
    {
        Console.WriteLine("═══ Top Series (by artwork count) ═══\n");

        var sql = @"SELECT series, COUNT(*) as count,
                           TO_CHAR(MIN(create_dt), 'YYYY') as earliest_year,
                           TO_CHAR(MAX(create_dt), 'YYYY') as latest_year
                    FROM artwork
                    WHERE series IS NOT NULL
                    GROUP BY series
                    ORDER BY count DESC
                    LIMIT 10";

        await PrintResults(sql);
    }

    private async Task ShowSalesSummary()
    {
        Console.WriteLine("═══ Sales Summary ═══\n");

        var sql = @"SELECT COUNT(*) as items_sold,
                           TO_CHAR(SUM(price), 'FM$999,999.99') as total_revenue,
                           TO_CHAR(AVG(price), 'FM$999,999.99') as avg_price,
                           TO_CHAR(MIN(price), 'FM$999,999.99') as lowest_sale,
                           TO_CHAR(MAX(price), 'FM$999,999.99') as highest_sale
                    FROM sold
                    WHERE price IS NOT NULL";

        await PrintResults(sql);
    }

    private async Task ShowRecentSales()
    {
        Console.WriteLine("═══ Recent Sales ═══\n");

        var sql = @"SELECT id_field as id,
                           collection as buyer,
                           TO_CHAR(price, 'FM$999,999.99') as price,
                           TO_CHAR(sale_dt, 'YYYY-MM-DD') as sale_date,
                           location
                    FROM sold
                    ORDER BY sale_dt DESC NULLS LAST
                    LIMIT 10";

        await PrintResults(sql);
    }

    private async Task ShowPhotographerStats()
    {
        Console.WriteLine("═══ Images by Photographer ═══\n");

        var sql = @"SELECT photographer, COUNT(*) as images
                    FROM artwork_image
                    WHERE photographer IS NOT NULL
                    GROUP BY photographer
                    ORDER BY images DESC
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

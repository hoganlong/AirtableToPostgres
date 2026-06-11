using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;

public class ShowAll
{
    private readonly string _connectionString;

    public ShowAll(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task Run(string inConStr)
    {
        var show = new ShowAll(inConStr!);

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Keith Long Archive - Data Insights              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await show.ShowRecentArtworks();
        await show.ShowArtworksByLocation();
        await show.ShowArtworksBySeries();
        await show.ShowCreationYears();
        await show.ShowSalesSummary();
        await show.ShowRecentSales();
        await show.ShowPhotographerStats();

        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("End of insights. For interactive queries, run:");
        Console.WriteLine("  dotnet run -- query");
        Console.WriteLine("  Or double-click: QueryExplorer.bat");
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
        Console.WriteLine();
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
        Console.WriteLine();
    }

    private async Task ShowArtworksBySeries()
    {
        Console.WriteLine("═══ Top 10 Series ═══\n");

        var sql = @"SELECT series, COUNT(*) as artworks,
                           TO_CHAR(MIN(create_dt), 'YYYY') as first,
                           TO_CHAR(MAX(create_dt), 'YYYY') as last
                    FROM artwork
                    WHERE series IS NOT NULL
                    GROUP BY series
                    ORDER BY artworks DESC
                    LIMIT 10";

        await PrintResults(sql);
        Console.WriteLine();
    }

    private async Task ShowCreationYears()
    {
        Console.WriteLine("═══ Artworks by Creation Year ═══\n");

        var sql = @"SELECT EXTRACT(YEAR FROM create_dt)::INT as year,
                           COUNT(*) as artworks
                    FROM artwork
                    WHERE create_dt IS NOT NULL
                    GROUP BY year
                    ORDER BY year DESC
                    LIMIT 15";

        await PrintResults(sql);
        Console.WriteLine();
    }

    private async Task ShowSalesSummary()
    {
        Console.WriteLine("═══ Sales Summary ═══\n");

        var sql = @"SELECT COUNT(*) as items_sold,
                           TO_CHAR(SUM(price), 'FM$999,999.99') as total_revenue,
                           TO_CHAR(AVG(price), 'FM$999,999.99') as avg_price,
                           TO_CHAR(MIN(price), 'FM$999,999.99') as lowest,
                           TO_CHAR(MAX(price), 'FM$999,999.99') as highest
                    FROM sold
                    WHERE price IS NOT NULL";

        await PrintResults(sql);
        Console.WriteLine();
    }

    private async Task ShowRecentSales()
    {
        Console.WriteLine("═══ Recent Sales ═══\n");

        var sql = @"SELECT id_field as id,
                           collection as buyer,
                           TO_CHAR(price, 'FM$999,999.99') as price,
                           TO_CHAR(sale_dt, 'YYYY-MM-DD') as sale_date
                    FROM sold
                    ORDER BY sale_dt DESC NULLS LAST
                    LIMIT 10";

        await PrintResults(sql);
        Console.WriteLine();
    }

    private async Task ShowPhotographerStats()
    {
        Console.WriteLine("═══ Top Photographers (by image count) ═══\n");

        var sql = @"SELECT photographer, COUNT(*) as images
                    FROM artwork_image
                    WHERE photographer IS NOT NULL
                    GROUP BY photographer
                    ORDER BY images DESC
                    LIMIT 10";

        await PrintResults(sql);
        Console.WriteLine();
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
            columnWidths[i] = Math.Min(columnWidths[i], 45);
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
                if (value.Length > 45)
                {
                    value = value.Substring(0, 42) + "...";
                }
                Console.Write(value.PadRight(columnWidths[i] + 2));
            }
            Console.WriteLine();
        }

        Console.WriteLine($"({table.Rows.Count} row{(table.Rows.Count != 1 ? "s" : "")})");
    }
}

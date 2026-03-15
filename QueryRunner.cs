using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;

public class QueryRunner
{
    private readonly string _connectionString;

    public QueryRunner(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task Run(string inConStr)
    {
        var runner = new QueryRunner(inConStr);

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        Keith Long Archive - Query Explorer                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("\nSelect a query to run:");
            Console.WriteLine("  1. Overview - Table counts");
            Console.WriteLine("  2. Recent artworks (last 15)");
            Console.WriteLine("  3. Artworks by location");
            Console.WriteLine("  4. Artworks by series");
            Console.WriteLine("  5. Artworks by creation year");
            Console.WriteLine("  6. Sales summary");
            Console.WriteLine("  7. All sold items");
            Console.WriteLine("  8. Sales by year");
            Console.WriteLine("  9. Images by photographer");
            Console.WriteLine(" 10. Artworks with most images");
            Console.WriteLine(" 11. Search artworks (custom)");
            Console.WriteLine(" 12. Search artworks (by human readable id)");
            Console.WriteLine(" 13. All art...");
            
            Console.WriteLine("  0. Exit");
            Console.WriteLine();
            Console.Write("Enter number: ");

            var choice = Console.ReadLine();
            Console.WriteLine();
            string? searchTerm = null;

            try
            {
                switch (choice)
                {
                    case "1":
                        await runner.RunQuery("Table Overview",
                            @"SELECT 'ARTWORK' as table_name, COUNT(*) as records FROM artwork
                              UNION ALL SELECT 'ARTWORK_IMAGE', COUNT(*) FROM artwork_image
                              UNION ALL SELECT 'PHOTO', COUNT(*) FROM photo
                              UNION ALL SELECT 'SOLD', COUNT(*) FROM sold
                              ORDER BY records DESC");
                        break;

                    case "2":
                        await runner.RunQuery("Recent Artworks",
                            @"SELECT id_field as id, title, series,
                                     TO_CHAR(create_dt, 'YYYY-MM-DD') as created,
                                     medium, location
                              FROM artwork
                              WHERE create_dt IS NOT NULL
                              ORDER BY create_dt DESC
                              LIMIT 15");
                        break;

                    case "3":
                        await runner.RunQuery("Artworks by Location",
                            @"SELECT location, COUNT(*) as count
                              FROM artwork
                              WHERE location IS NOT NULL
                              GROUP BY location
                              ORDER BY count DESC");
                        break;

                    case "4":
                        await runner.RunQuery("Artworks by Series",
                            @"SELECT series, COUNT(*) as count,
                                     TO_CHAR(MIN(create_dt), 'YYYY-MM-DD') as earliest,
                                     TO_CHAR(MAX(create_dt), 'YYYY-MM-DD') as latest
                              FROM artwork
                              WHERE series IS NOT NULL
                              GROUP BY series
                              ORDER BY count DESC
                              LIMIT 10");
                        break;

                    case "5":
                        await runner.RunQuery("Artworks by Year",
                            @"SELECT EXTRACT(YEAR FROM create_dt)::INT as year,
                                     COUNT(*) as artworks_created
                              FROM artwork
                              WHERE create_dt IS NOT NULL
                              GROUP BY year
                              ORDER BY year DESC");
                        break;

                    case "6":
                        await runner.RunQuery("Sales Summary",
                            @"SELECT COUNT(*) as items_sold,
                                     TO_CHAR(SUM(price), '$999,999.99') as total_revenue,
                                     TO_CHAR(AVG(price), '$999,999.99') as avg_price,
                                     TO_CHAR(MIN(price), '$999,999.99') as lowest_sale,
                                     TO_CHAR(MAX(price), '$999,999.99') as highest_sale
                              FROM sold
                              WHERE price IS NOT NULL");
                        break;

                    case "7":
                        await runner.RunQuery("All Sold Items",
                            @"SELECT id_field as id, collection as buyer,
                                     price, TO_CHAR(sale_dt, 'YYYY-MM-DD') as sale_date,
                                     location
                              FROM sold
                              ORDER BY sale_dt DESC");
                        break;

                    case "8":
                        await runner.RunQuery("Sales by Year",
                            @"SELECT EXTRACT(YEAR FROM sale_dt)::INT as year,
                                     COUNT(*) as items_sold,
                                     TO_CHAR(SUM(price), '$999,999.99') as revenue
                              FROM sold
                              WHERE sale_dt IS NOT NULL AND price IS NOT NULL
                              GROUP BY year
                              ORDER BY year DESC");
                        break;

                    case "9":
                        await runner.RunQuery("Images by Photographer",
                            @"SELECT photographer, COUNT(*) as images
                              FROM artwork_image
                              WHERE photographer IS NOT NULL
                              GROUP BY photographer
                              ORDER BY images DESC");
                        break;

                    case "10":
                        await runner.RunQuery("Artworks with Most Images",
                            @"SELECT id_field as id, title,
                                     jsonb_array_length(artwork_image_id) as num_images
                              FROM artwork
                              WHERE artwork_image_id IS NOT NULL
                                AND jsonb_array_length(artwork_image_id) > 0
                              ORDER BY jsonb_array_length(artwork_image_id) DESC
                              LIMIT 10");
                        break;

                    case "11":
                        searchTerm = Console.ReadLine();
                        Console.Write("Enter search term (title or medium): ");
                        await runner.RunQuery($"Search Results for '{searchTerm}'",
                            $@"SELECT id_field as id, title, series, medium, location
                               FROM artwork
                               WHERE title ILIKE '%{searchTerm}%'
                                  OR medium ILIKE '%{searchTerm}%'
                               ORDER BY create_dt DESC
                               LIMIT 20");
                        break;

                    case "12":
                        Console.Write("Enter search term (human readable ID): ");
                        searchTerm = Console.ReadLine();
                        await runner.RunQuery($"Search Results for human readable '{searchTerm}'",
                            $@"SELECT *
                               FROM artwork
                               WHERE human_readable_id ILIKE '%{searchTerm}%'
                               ORDER BY create_dt DESC
                               LIMIT 20");
                        break;


                    case "13":
                        await runner.RunQuery($"Art table.",
                            $@"SELECT *
                               FROM artwork
                               ORDER BY create_dt DESC
                               LIMIT 1000");
                        break;

                    case "0":
                        Console.WriteLine("Goodbye!");
                        return;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║        Keith Long Archive - Query Explorer                ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
    }

    public async Task RunQuery(string title, string sql)
    {
        Console.WriteLine($"═══ {title} ═══");
        Console.WriteLine();

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
            columnWidths[i] = Math.Min(columnWidths[i], 50); // Max width 50
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

        Console.WriteLine();
        Console.WriteLine($"({table.Rows.Count} row{(table.Rows.Count != 1 ? "s" : "")})");
    }
}

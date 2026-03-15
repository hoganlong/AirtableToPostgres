using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace AirtableToPostgres;


public static class DataTableExtensions
{
    public static void PrintToConsoleAligned(this DataTable dt)
    {
        if (dt == null) return;

        // 1. Calculate maximum column widths
        var columnWidths = dt.Columns.Cast<DataColumn>()
            .Select(col => Math.Max(col.ColumnName.Length, dt.AsEnumerable().Select(row => row[col].ToString() ?? "")
                .Max(str => str.Length)))
            .ToList();

        // 2. Print the header row
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            Console.Write(dt.Columns[i].ColumnName.PadRight(columnWidths[i] + 2)); // Add 2 for spacing
        }
        Console.WriteLine();
        Console.WriteLine(new string('-', columnWidths.Sum() + (columnWidths.Count * 2)));

        // 3. Print the data rows
        foreach (DataRow row in dt.Rows)
        {
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                string x = row[i].ToString() ?? "";
                // Align left using PadRight
                Console.Write(x.PadRight(columnWidths[i] + 2));
            }
            Console.WriteLine();
        }
    }
}


public class QuickTest
{
    public static async Task Run(string inConStr)
    {
        Console.WriteLine("═══ Table Overview New ═══\n");

        await using var connection = new NpgsqlConnection(inConStr);
        await connection.OpenAsync();

        var sql = @"
           -- Count rows of all tables inside a PostgreSQL database
           WITH tbl AS (
            SELECT * 
            FROM information_schema.tables 
            WHERE table_catalog = 'keithlongarchive' AND table_type = 'BASE TABLE'  AND 
                  table_name NOT LIKE 'pg_%' AND table_schema NOT LIKE 'information_schema'
           )
           SELECT 
             tbl.table_catalog AS ""Database"",
             tbl.table_schema AS ""SCHEMA"",
             tbl.table_name AS ""TABLE_NAME"",
             (tbl.table_schema || '.' || tbl.table_name) AS ""caller"",
             pg_total_relation_size(tbl.table_schema || '.' || tbl.table_name) / 1024 AS size_kb , 
             (pg_total_relation_size(tbl.table_schema || '.' || tbl.table_name))::real / 1024 / 1024 AS size_mb ,
             s.n_live_tup AS estimated_row_count,
             (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I.%I', tbl.table_schema, tbl.table_name), FALSE, TRUE, '')))[1]::text::bigint AS ""true_row_count""
           FROM tbl 
           INNER JOIN pg_stat_user_tables s ON tbl.table_name = s.relname
           ORDER BY caller;";
/*
        var sql = @"SELECT
                      schemaname AS table_schema,
                      relname AS table_name,
                      n_live_tup AS estimated_row_count
                    FROM pg_stat_user_tables
                    ORDER BY n_live_tup DESC;";
*/
/*
        var sql = @"SELECT 'ARTWORK' as table_name, COUNT(*) as records FROM artwork
                    UNION ALL SELECT 'ARTWORK_IMAGE', COUNT(*) FROM artwork_image
                    UNION ALL SELECT 'PHOTO', COUNT(*) FROM photo
                    UNION ALL SELECT 'SOLD', COUNT(*) FROM sold
                    UNION ALL SELECT 'ARCHIVE', COUNT(*) FROM archive
                    UNION ALL SELECT 'SKETCH', COUNT(*) FROM sketch
                    ORDER BY records DESC";
*/
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var table = new DataTable();
        table.Load(reader);

        table.PrintToConsoleAligned();
     
/*




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
        Console.WriteLine("  Or run: dotnet run -- query");*/
    }
}

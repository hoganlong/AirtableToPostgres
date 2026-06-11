using AirtableToPostgres;

namespace AirtableToPostgres.Tests;

/// <summary>
/// Quick test class to verify schema parsing and DDL generation
/// Run this by temporarily calling TestSchemaParser.Run() from Main
/// </summary>
public static class TestSchemaParser
{
    public static void Run()
    {
        Console.WriteLine("=== Schema Parser Test ===\n");

        // Parse schema file
        var parser = new SchemaParser();
        var schema = parser.Parse("airtable_schema.txt");

        Console.WriteLine($"Generated Date: {schema.GeneratedDate}");
        Console.WriteLine($"Total Tables: {schema.Tables.Count}\n");

        // Find ARTWORK table
        var artworkTable = schema.Tables.FirstOrDefault(t => t.Name == "ARTWORK");
        if (artworkTable == null)
        {
            Console.WriteLine("ERROR: ARTWORK table not found!");
            return;
        }

        Console.WriteLine($"Table: {artworkTable.Name}");
        Console.WriteLine($"Airtable ID: {artworkTable.AirtableId}");
        Console.WriteLine($"Fields: {artworkTable.Fields.Count}\n");

        // Test type mapping
        var typeMapper = new TypeMapper();

        Console.WriteLine("=== Field Type Mappings ===\n");
        Console.WriteLine($"{"Field Name",-30} {"Airtable Type",-25} {"PostgreSQL Type",-20}");
        Console.WriteLine(new string('-', 75));

        foreach (var field in artworkTable.Fields)
        {
            var pgType = typeMapper.MapFieldType(field);
            var sanitizedName = SchemaGenerator.SanitizeColumnName(field.Name);
            Console.WriteLine($"{field.Name,-30} {field.Type,-25} {pgType,-20}");

            if (field.Name != sanitizedName)
            {
                Console.WriteLine($"  → Sanitized to: {sanitizedName}");
            }
        }

        // Test DDL generation
        Console.WriteLine("\n=== Generated DDL ===\n");
        var schemaGenerator = new SchemaGenerator(typeMapper);
        var ddl = schemaGenerator.GenerateCreateTableDdl(artworkTable, "artwork");
        Console.WriteLine(ddl);

        Console.WriteLine("\n=== Test Complete ===");
    }
}

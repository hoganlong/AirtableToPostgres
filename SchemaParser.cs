using Newtonsoft.Json.Linq;
using System.Globalization;

namespace AirtableToPostgres;

public class AirtableSchema
{
    public DateTime GeneratedDate { get; set; }
    public List<TableSchema> Tables { get; set; } = new();
}

public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public string AirtableId { get; set; } = string.Empty;
    public List<FieldSchema> Fields { get; set; } = new();
}

public class FieldSchema
{
    public string Name { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JObject? Options { get; set; }
}

public class SchemaParser
{
    public AirtableSchema Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Schema file not found: {filePath}");
        }

        var schema = new AirtableSchema();
        var lines = File.ReadAllLines(filePath);

        TableSchema? currentTable = null;
        FieldSchema? currentField = null;
        var state = ParseState.Header;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Parse header line for generation date
            if (i == 1 && line.StartsWith("Generated:"))
            {
                var dateStr = line.Substring("Generated:".Length).Trim();
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    schema.GeneratedDate = date;
                }
                continue;
            }

            // Parse table header
            if (line.StartsWith("TABLE:"))
            {
                // Flush last field into table before saving
                if (currentField != null && currentTable != null)
                {
                    currentTable.Fields.Add(currentField);
                    currentField = null;
                }
                // Save previous table if exists
                if (currentTable != null)
                {
                    schema.Tables.Add(currentTable);
                }

                currentTable = new TableSchema
                {
                    Name = line.Substring("TABLE:".Length).Trim()
                };
                state = ParseState.TableHeader;
                continue;
            }

            // Parse table ID
            if (state == ParseState.TableHeader && line.StartsWith("ID:"))
            {
                if (currentTable != null)
                {
                    currentTable.AirtableId = line.Substring("ID:".Length).Trim();
                }
                state = ParseState.Fields;
                continue;
            }

            // Parse field name
            if (line.StartsWith("  Field:"))
            {
                // Save previous field if exists
                if (currentField != null && currentTable != null)
                {
                    currentTable.Fields.Add(currentField);
                }

                currentField = new FieldSchema
                {
                    Name = line.Substring("  Field:".Length).Trim()
                };
                state = ParseState.FieldProperties;
                continue;
            }

            // Parse field properties
            if (state == ParseState.FieldProperties && line.StartsWith("    "))
            {
                var propertyLine = line.Trim();

                if (propertyLine.StartsWith("ID:"))
                {
                    if (currentField != null)
                    {
                        currentField.FieldId = propertyLine.Substring("ID:".Length).Trim();
                    }
                }
                else if (propertyLine.StartsWith("Type:"))
                {
                    if (currentField != null)
                    {
                        currentField.Type = propertyLine.Substring("Type:".Length).Trim();
                    }
                }
                else if (propertyLine.StartsWith("Description:"))
                {
                    if (currentField != null)
                    {
                        currentField.Description = propertyLine.Substring("Description:".Length).Trim();
                    }
                }
                else if (propertyLine.StartsWith("Options:"))
                {
                    if (currentField != null)
                    {
                        var optionsJson = propertyLine.Substring("Options:".Length).Trim();
                        try
                        {
                            currentField.Options = JObject.Parse(optionsJson);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to parse options for field '{currentField.Name}': {ex.Message}");
                        }
                    }
                }
            }
        }

        // Add last field and table
        if (currentField != null && currentTable != null)
        {
            currentTable.Fields.Add(currentField);
        }
        if (currentTable != null)
        {
            schema.Tables.Add(currentTable);
        }

        return schema;
    }

    private enum ParseState
    {
        Header,
        TableHeader,
        Fields,
        FieldProperties
    }
}

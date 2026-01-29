using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace AirtableToPostgres;

public class TypeMapper
{
    private readonly Dictionary<string, string> _typeMappings;
    private readonly HashSet<string> _loggedFields = new();

    public TypeMapper(IConfiguration? configuration = null)
    {
        // Default type mappings
        _typeMappings = new Dictionary<string, string>
        {
            { "autoNumber", "INTEGER" },
            { "singleLineText", "TEXT" },
            { "multilineText", "TEXT" },
            { "number", "NUMERIC" },
            { "currency", "NUMERIC(19,4)" },
            { "date", "DATE" },
            { "createdTime", "TIMESTAMP WITH TIME ZONE" },
            { "singleSelect", "TEXT" },
            { "url", "TEXT" },
            { "formula", "TEXT" },
            { "count", "INTEGER" },
            { "multipleRecordLinks", "JSONB" },
            { "multipleAttachments", "JSONB" },
            { "multipleLookupValues", "JSONB" }
        };

        // Load custom overrides from configuration if provided
        if (configuration != null)
        {
            var customMappings = configuration.GetSection("Schema:TypeMappings");
            foreach (var mapping in customMappings.GetChildren())
            {
                _typeMappings[mapping.Key] = mapping.Value ?? "TEXT";
            }
        }
    }

    public string MapFieldType(FieldSchema field)
    {
        // Handle formula fields specially - check result type
        if (field.Type == "formula")
        {
            return MapFormulaType(field);
        }

        // Handle number fields with precision
        if (field.Type == "number")
        {
            return MapNumberType(field);
        }

        // Handle lookup fields - check result type
        if (field.Type == "multipleLookupValues")
        {
            return MapLookupType(field);
        }

        // Use default mapping
        if (_typeMappings.TryGetValue(field.Type, out var pgType))
        {
            return pgType;
        }

        // Fallback to TEXT for unknown types
        Console.WriteLine($"Warning: Unknown field type '{field.Type}' for field '{field.Name}', defaulting to TEXT");
        return "TEXT";
    }

    private string MapFormulaType(FieldSchema field)
    {
        if (field.Options != null && field.Options["result"] is JObject result)
        {
            var resultType = result["type"]?.ToString();

            switch (resultType)
            {
                case "singleLineText":
                    return "TEXT";
                case "number":
                    // Check if result has precision
                    if (result["options"] is JObject options &&
                        options["precision"] != null)
                    {
                        var precision = options["precision"]?.Value<int>() ?? 0;
                        return precision == 0 ? "INTEGER" : "NUMERIC";
                    }
                    return "NUMERIC";
                case "date":
                    return "DATE";
                case "dateTime":
                    return "TIMESTAMP WITH TIME ZONE";
                default:
                    Console.WriteLine($"Warning: Unknown formula result type '{resultType}' for field '{field.Name}', defaulting to TEXT");
                    return "TEXT";
            }
        }

        return "TEXT";
    }

    private string MapNumberType(FieldSchema field)
    {
        if (field.Options != null && field.Options["precision"] != null)
        {
            var precision = field.Options["precision"]?.Value<int>() ?? 0;
            return precision == 0 ? "INTEGER" : "NUMERIC";
        }

        return "NUMERIC";
    }

    private string MapLookupType(FieldSchema field)
    {
        // For lookup fields, we could check the result type
        // but for safety and flexibility, we'll default to JSONB
        // This allows for arrays of values and complex data

        if (field.Options != null && field.Options["result"] is JObject result)
        {
            var resultType = result["type"]?.ToString();

            // Only log once per field to avoid spam
            if (_loggedFields.Add(field.Name))
            {
                Console.WriteLine($"    Info: Lookup field '{field.Name}' has result type '{resultType}', using JSONB");
            }
        }

        return "JSONB";
    }
}

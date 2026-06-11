using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AirtableToPostgres;

public class RecordMapper
{
    private readonly TableSchema _tableSchema;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _jsonbColumns;
    private readonly HashSet<string> _trimColumns;

    public RecordMapper(TableSchema tableSchema, TypeMapper typeMapper, IConfiguration? configuration = null)
    {
        _tableSchema = tableSchema;
        _typeMapper = typeMapper;

        // Pre-compute which columns are JSONB type
        _jsonbColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in tableSchema.Fields)
        {
            var pgType = typeMapper.MapFieldType(field, tableSchema.Name);
            if (pgType == "JSONB")
            {
                var columnName = SchemaGenerator.SanitizeColumnName(field.Name);
                _jsonbColumns.Add(columnName);
            }
        }

        // Load trim fields from config: Schema:TrimFields:<TABLENAME>
        _trimColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (configuration != null)
        {
            var trimSection = configuration.GetSection($"Schema:TrimFields:{tableSchema.Name}");
            foreach (var entry in trimSection.GetChildren())
            {
                if (entry.Value != null)
                    _trimColumns.Add(SchemaGenerator.SanitizeColumnName(entry.Value));
            }
        }
    }

    public bool IsJsonbColumn(string columnName)
    {
        return _jsonbColumns.Contains(columnName);
    }

    public Dictionary<string, object?> MapRecordToColumns(JObject airtableRecord)
    {
        var result = new Dictionary<string, object?>();

        // Add metadata fields
        result["airtable_id"] = airtableRecord["id"]?.ToString() ?? throw new InvalidOperationException("Record missing id");

        var createdTimeStr = airtableRecord["createdTime"]?.ToString();
        result["rec_create_dt"] = ParseDateTime(createdTimeStr) ?? throw new InvalidOperationException("Record missing createdTime");

        result["synced_at"] = DateTime.UtcNow;

        // Get the fields object
        var fields = airtableRecord["fields"] as JObject;

        // Map each schema field
        foreach (var field in _tableSchema.Fields)
        {
            var columnName = SchemaGenerator.SanitizeColumnName(field.Name);
            var rawValue = fields?[field.Name];

            result[columnName] = MapFieldValue(rawValue, field);
        }

        return result;
    }

    private object? MapFieldValue(JToken? rawValue, FieldSchema field)
    {
        if (rawValue == null || rawValue.Type == JTokenType.Null)
        {
            return DBNull.Value;
        }

        try
        {
            var pgType = _typeMapper.MapFieldType(field, _tableSchema.Name);

            // Handle JSONB types - keep as JSON
            if (pgType == "JSONB")
            {
                return JsonConvert.SerializeObject(rawValue);
            }

            // Handle text types (TEXT or VARCHAR)
            if (pgType == "TEXT" || pgType.StartsWith("VARCHAR"))
            {
                // Special case: multipleRecordLinks mapped to TEXT/VARCHAR (single value expected)
                if (field.Type == "multipleRecordLinks" && rawValue is JArray array)
                {
                    if (array.Count == 0)
                    {
                        return DBNull.Value;
                    }
                    // Extract first (and expected only) value from array
                    return array[0].ToString();
                }
                var strValue = rawValue.ToString();
                var columnName = SchemaGenerator.SanitizeColumnName(field.Name);
                if (_trimColumns.Contains(columnName))
                    strValue = strValue.Trim();
                return strValue;
            }

            // Handle INTEGER
            if (pgType == "INTEGER")
            {
                if (rawValue.Type == JTokenType.Integer)
                {
                    return rawValue.Value<int>();
                }
                if (rawValue.Type == JTokenType.Float)
                {
                    return (int)rawValue.Value<double>();
                }
                if (int.TryParse(rawValue.ToString(), out var intValue))
                {
                    return intValue;
                }
                Console.WriteLine($"Warning: Could not parse '{rawValue}' as INTEGER for field '{field.Name}'");
                return DBNull.Value;
            }

            // Handle NUMERIC
            if (pgType.StartsWith("NUMERIC"))
            {
                if (rawValue.Type == JTokenType.Integer || rawValue.Type == JTokenType.Float)
                {
                    return rawValue.Value<decimal>();
                }
                if (decimal.TryParse(rawValue.ToString(), out var decimalValue))
                {
                    return decimalValue;
                }
                Console.WriteLine($"Warning: Could not parse '{rawValue}' as NUMERIC for field '{field.Name}'");
                return DBNull.Value;
            }

            // Handle DATE
            if (pgType == "DATE")
            {
                var dateTime = ParseDateTime(rawValue.ToString());
                if (dateTime.HasValue)
                {
                    return dateTime.Value.Date;
                }
                Console.WriteLine($"Warning: Could not parse '{rawValue}' as DATE for field '{field.Name}'");
                return DBNull.Value;
            }

            // Handle TIMESTAMP WITH TIME ZONE
            if (pgType == "TIMESTAMP WITH TIME ZONE")
            {
                var dateTime = ParseDateTime(rawValue.ToString());
                if (dateTime.HasValue)
                {
                    return dateTime.Value;
                }
                Console.WriteLine($"Warning: Could not parse '{rawValue}' as TIMESTAMP for field '{field.Name}'");
                return DBNull.Value;
            }

            // Handle BOOLEAN
            if (pgType == "BOOLEAN")
            {
                if (rawValue.Type == JTokenType.Boolean)
                {
                    return rawValue.Value<bool>();
                }
                var strVal = rawValue.ToString().ToLower();
                if (strVal == "true" || strVal == "1" || strVal == "yes") return true;
                if (strVal == "false" || strVal == "0" || strVal == "no") return false;
                Console.WriteLine($"Warning: Could not parse '{rawValue}' as BOOLEAN for field '{field.Name}'");
                return DBNull.Value;
            }

            // Fallback to string
            return rawValue.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to convert field '{field.Name}' with value '{rawValue}': {ex.Message}");
            return DBNull.Value;
        }
    }

    private DateTime? ParseDateTime(string? dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr))
        {
            return null;
        }

        // Try parsing ISO 8601 format (Airtable default)
        if (DateTime.TryParse(dateTimeStr, out var result))
        {
            // Ensure UTC
            if (result.Kind == DateTimeKind.Unspecified)
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            }
            return result;
        }

        return null;
    }
}

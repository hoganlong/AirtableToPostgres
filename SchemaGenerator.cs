using System.Text;
using System.Text.RegularExpressions;

namespace AirtableToPostgres;

public class SchemaGenerator
{
    private readonly TypeMapper _typeMapper;
    private static readonly HashSet<string> PostgresReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "user", "order", "table", "column", "index", "select", "insert", "update", "delete",
        "create", "alter", "drop", "grant", "revoke", "where", "from", "group", "having",
        "join", "left", "right", "inner", "outer", "union", "all", "distinct", "as",
        "and", "or", "not", "null", "true", "false", "default", "primary", "foreign",
        "key", "references", "constraint", "check", "unique", "index"
    };

    public SchemaGenerator(TypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }

    public string GenerateCreateTableDdl(TableSchema tableSchema, string postgresTableName)
    {
        var sb = new StringBuilder();

        // Check if schema already provides rec_create_dt (e.g. via Airtable createdTime field)
        var hasRecCreateDtInSchema = tableSchema.Fields.Any(f => SanitizeColumnName(f.Name) == "rec_create_dt");

        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {postgresTableName} (");
        sb.AppendLine("    id SERIAL PRIMARY KEY,");
        sb.AppendLine("    airtable_id VARCHAR(255) UNIQUE NOT NULL,");

        // Add custom fields
        foreach (var field in tableSchema.Fields)
        {
            var columnName = SanitizeColumnName(field.Name);
            var columnType = _typeMapper.MapFieldType(field, tableSchema.Name);

            sb.AppendLine($"    {columnName} {columnType} NULL,");
        }

        // Add metadata fields (rec_create_dt only if schema doesn't already define it)
        if (!hasRecCreateDtInSchema)
        {
            sb.AppendLine("    rec_create_dt TIMESTAMP WITH TIME ZONE NOT NULL,");
        }
        sb.AppendLine("    synced_at TIMESTAMP WITH TIME ZONE NOT NULL,");
        sb.AppendLine("    last_modified_at TIMESTAMP WITH TIME ZONE NULL");
        sb.AppendLine(");");

        // Add index on airtable_id
        sb.AppendLine();
        sb.AppendLine($"CREATE INDEX IF NOT EXISTS idx_{postgresTableName}_airtable_id");
        sb.AppendLine($"    ON {postgresTableName}(airtable_id);");

        // Add migration: ensure all schema columns exist (safe for existing tables)
        sb.AppendLine();
        foreach (var field in tableSchema.Fields)
        {
            var columnName = SanitizeColumnName(field.Name);
            var columnType = _typeMapper.MapFieldType(field, tableSchema.Name);
            sb.AppendLine($"ALTER TABLE {postgresTableName} ADD COLUMN IF NOT EXISTS {columnName} {columnType} NULL;");
        }
        sb.AppendLine($"ALTER TABLE {postgresTableName} ADD COLUMN IF NOT EXISTS rec_create_dt TIMESTAMP WITH TIME ZONE NULL;");
        sb.AppendLine($"ALTER TABLE {postgresTableName} ADD COLUMN IF NOT EXISTS last_modified_at TIMESTAMP WITH TIME ZONE NULL;");
        sb.AppendLine($"ALTER TABLE {postgresTableName} DROP COLUMN IF EXISTS created_time;");

        return sb.ToString();
    }

    public static string SanitizeColumnName(string fieldName)
    {
        // Convert to lowercase
        var sanitized = fieldName.ToLowerInvariant();

        // Replace spaces with underscores
        sanitized = sanitized.Replace(' ', '_');

        // Remove or replace parentheses and their contents
        // "Code (from TYPE)" -> "code_from_type"
        sanitized = Regex.Replace(sanitized, @"\s*\([^)]*\)", match =>
        {
            // Extract content inside parentheses
            var content = match.Value.Trim();
            if (content.Length > 2)
            {
                content = content.Substring(1, content.Length - 2).Trim();
                return "_" + content.Replace(' ', '_').ToLowerInvariant();
            }
            return "";
        });

        // Replace any remaining special characters with underscores
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9_]", "_");

        // Remove consecutive underscores
        sanitized = Regex.Replace(sanitized, @"_+", "_");

        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');

        // Handle empty names or numeric-only names
        if (string.IsNullOrEmpty(sanitized) || Regex.IsMatch(sanitized, @"^\d"))
        {
            sanitized = "field_" + sanitized;
        }

        // Handle PostgreSQL reserved keywords
        if (PostgresReservedWords.Contains(sanitized))
        {
            sanitized = "at_" + sanitized;
        }

        // Special case: if the original name was "ID", rename to avoid conflict with serial id
        if (fieldName.Equals("ID", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = "id_field";
        }

        return sanitized;
    }

    public static string SanitizeTableName(string tableName)
    {
        // Similar to column name sanitization but for table names
        var sanitized = tableName.ToLowerInvariant();
        sanitized = sanitized.Replace(' ', '_');
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9_]", "_");
        sanitized = Regex.Replace(sanitized, @"_+", "_");
        sanitized = sanitized.Trim('_');

        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "table";
        }

        // Handle PostgreSQL reserved keywords
        if (PostgresReservedWords.Contains(sanitized))
        {
            sanitized = "at_" + sanitized;
        }

        return sanitized;
    }
}

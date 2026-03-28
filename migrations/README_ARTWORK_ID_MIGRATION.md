# ARTWORK_ID Column Type Conversion

## Summary
Convert `artwork_image.artwork_id` from JSONB to VARCHAR(255) because it only ever contains a single string value.

## Changes Made

### 1. Database Migration Script
**File**: `migrations/001_convert_artwork_id_to_string.sql`

This script will:
- Add a new `artwork_id_new` column as VARCHAR(255)
- Extract the single string value from the JSONB array
- Drop the old JSONB column
- Rename the new column to `artwork_id`
- Add an index for performance

**To run**:
```sql
psql -h keithlong-archive.cluster-cwlmm0oodeot.us-east-1.rds.amazonaws.com \
     -U hoganlong \
     -d keithlongarchive \
     -f migrations/001_convert_artwork_id_to_string.sql
```

### 2. ETL Program Updates
**Files Modified**:
- `TypeMapper.cs` - Added field-specific mapping support
- `SchemaGenerator.cs` - Passes table name to type mapper
- `RecordMapper.cs` - Handles multipleRecordLinks mapped to TEXT
- `appsettings.json` - Configured ARTWORK_IMAGE.ARTWORK_ID as VARCHAR(255)

**Key Feature**: Field-Specific Type Mappings

The TypeMapper now supports overriding types for specific fields via configuration:

```json
"Schema": {
  "FieldMappings": {
    "ARTWORK_IMAGE": {
      "ARTWORK_ID": "VARCHAR(255)"
    }
  }
}
```

This allows fields that are normally JSONB (like `multipleRecordLinks`) to be stored as simple VARCHAR when they only contain single values.

## Execution Steps

### Step 1: Backup (IMPORTANT!)
```sql
-- Create backup of artwork_image table
CREATE TABLE artwork_image_backup_20260215 AS
SELECT * FROM artwork_image;
```

### Step 2: Run Migration
Execute the migration script:
```bash
cd /d/Projects/claudetest/AirtableToPostgres
psql -h keithlong-archive.cluster-cwlmm0oodeot.us-east-1.rds.amazonaws.com \
     -U hoganlong \
     -d keithlongarchive \
     -f migrations/001_convert_artwork_id_to_string.sql
```

Or connect with psql and run the script interactively:
```sql
\i migrations/001_convert_artwork_id_to_string.sql
```

### Step 3: Verify Migration
After running the migration, verify the conversion:

```sql
-- Check column type
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_name = 'artwork_image'
  AND column_name = 'artwork_id';

-- Expected result:
-- column_name | data_type | character_maximum_length
-- artwork_id  | varchar   | 255

-- Check data samples
SELECT id, artwork_id, airtable_id
FROM artwork_image
LIMIT 10;

-- Count records with/without artwork_id
SELECT
  COUNT(*) as total_records,
  COUNT(artwork_id) as records_with_artwork_id,
  COUNT(*) - COUNT(artwork_id) as records_without_artwork_id
FROM artwork_image;
```

### Step 4: Test ETL Program
Run a sync to ensure the ETL program handles the new schema:

```bash
cd /d/Projects/claudetest/AirtableToPostgres
dotnet run
```

Watch for:
- ✅ ARTWORK_IMAGE table syncs successfully
- ✅ artwork_id column receives string values (not JSONB)
- ✅ No errors during record mapping

### Step 5: Verify ETL Results
After sync completes:

```sql
-- Check newly synced records
SELECT id, artwork_id, airtable_id, synced_at
FROM artwork_image
ORDER BY synced_at DESC
LIMIT 10;

-- Verify artwork_id is populated correctly
SELECT
  artwork_id,
  COUNT(*) as count
FROM artwork_image
WHERE artwork_id IS NOT NULL
GROUP BY artwork_id
ORDER BY count DESC
LIMIT 20;
```

## Rollback Plan

If something goes wrong:

```sql
-- Drop the modified table
DROP TABLE artwork_image;

-- Restore from backup
ALTER TABLE artwork_image_backup_20260215
RENAME TO artwork_image;

-- Restore indexes
CREATE INDEX IF NOT EXISTS idx_artwork_image_airtable_id
  ON artwork_image(airtable_id);
```

Then revert the appsettings.json changes:
```json
"FieldMappings": {
  // Remove or comment out the ARTWORK_IMAGE mapping
}
```

## Technical Details

### How the ETL Handles the Conversion

When Airtable sends a `multipleRecordLinks` field:
```json
{
  "ARTWORK_ID": ["rec123ABC456DEF"]
}
```

The RecordMapper now:
1. Detects it's a `multipleRecordLinks` field mapped to TEXT
2. Extracts the first (and only) value from the array
3. Stores it as a simple VARCHAR: `"rec123ABC456DEF"`

### Migration Script Details

The script uses a safe multi-step approach:
1. Add new column (non-destructive)
2. Migrate data with verification step
3. Only drop old column after verification
4. Rename to final name

The JSONB extraction uses:
```sql
artwork_id->0->>0
```
Which means:
- `->0` - Get first element of JSON array as JSON
- `->>0` - Get that value as text

## Future Considerations

### Other Candidate Fields

You may want to apply this same conversion to other fields that are `multipleRecordLinks` but only contain single values:

- Check `ARCHIVE_IMAGE.ARCHIVE_ID`
- Check `SOLD.ARTWORK_ID`
- Check `SOLD.ARCHIVE_ID`

To verify if they're single-value:
```sql
SELECT
  jsonb_array_length(artwork_id) as array_length,
  COUNT(*) as count
FROM artwork_image
WHERE artwork_id IS NOT NULL
GROUP BY jsonb_array_length(artwork_id);
```

If all results show `array_length = 1`, the field is a candidate for VARCHAR conversion.

### Adding More Field Mappings

To convert additional fields, update `appsettings.json`:

```json
"FieldMappings": {
  "ARTWORK_IMAGE": {
    "ARTWORK_ID": "VARCHAR(255)"
  },
  "ARCHIVE_IMAGE": {
    "ARCHIVE_ID": "VARCHAR(255)"
  },
  "SOLD": {
    "ARTWORK_ID": "VARCHAR(255)",
    "ARCHIVE_ID": "VARCHAR(255)"
  }
}
```

Then create and run migration scripts for each field.

## Status

- ✅ Code changes complete
- ✅ Migration script created
- ✅ Build successful
- ⏳ **Migration pending execution**
- ⏳ **ETL testing pending**

## Next Steps

1. **Run the migration** (see Step 2 above)
2. **Verify the migration** (see Step 3 above)
3. **Test the ETL** (see Step 4 above)
4. **Verify ETL results** (see Step 5 above)

---

**Created**: 2026-02-15
**Author**: Claude Sonnet 4.5

-- Verify the artwork table structure
SELECT
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'artwork'
ORDER BY ordinal_position;

-- Count total records
SELECT COUNT(*) as total_records FROM artwork;

-- View sample records with key fields
SELECT
    id,
    airtable_id,
    id_field,
    title,
    series,
    create_dt,
    medium,
    location,
    type_number,
    synced_at
FROM artwork
LIMIT 10;

-- Check for records with JSONB data
SELECT
    id_field,
    title,
    jsonb_array_length(type_id) as type_count,
    jsonb_array_length(photo_id) as photo_count
FROM artwork
WHERE type_id IS NOT NULL
   OR photo_id IS NOT NULL
LIMIT 10;

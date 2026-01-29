-- Verify all synced tables from Airtable

-- Table summary
SELECT
    'artwork' as table_name,
    COUNT(*) as record_count
FROM artwork
UNION ALL
SELECT
    'artwork_image',
    COUNT(*)
FROM artwork_image
UNION ALL
SELECT
    'archive',
    COUNT(*)
FROM archive
UNION ALL
SELECT
    'archive_image',
    COUNT(*)
FROM archive_image
UNION ALL
SELECT
    'sold',
    COUNT(*)
FROM sold
UNION ALL
SELECT
    'artwork_type',
    COUNT(*)
FROM artwork_type
UNION ALL
SELECT
    'photo',
    COUNT(*)
FROM photo
UNION ALL
SELECT
    'photo_catagory',
    COUNT(*)
FROM photo_catagory
ORDER BY table_name;

-- Total records across all tables
SELECT
    SUM(record_count) as total_records
FROM (
    SELECT COUNT(*) as record_count FROM artwork
    UNION ALL SELECT COUNT(*) FROM artwork_image
    UNION ALL SELECT COUNT(*) FROM archive
    UNION ALL SELECT COUNT(*) FROM archive_image
    UNION ALL SELECT COUNT(*) FROM sold
    UNION ALL SELECT COUNT(*) FROM artwork_type
    UNION ALL SELECT COUNT(*) FROM photo
    UNION ALL SELECT COUNT(*) FROM photo_catagory
) counts;

-- Sample data from each table

-- ARTWORK samples
SELECT 'ARTWORK' as table_name, id_field, title, series, medium, location
FROM artwork
LIMIT 3;

-- ARTWORK_IMAGE samples
SELECT 'ARTWORK_IMAGE' as table_name, id_field, create_dt, photographer, view
FROM artwork_image
LIMIT 3;

-- SOLD samples
SELECT 'SOLD' as table_name, id_field, collection, price, sale_dt
FROM sold
LIMIT 3;

-- ARTWORK_TYPE samples
SELECT 'ARTWORK_TYPE' as table_name, code, description
FROM artwork_type
LIMIT 3;

-- PHOTO samples
SELECT 'PHOTO' as table_name, id_field, description, year, location
FROM photo
LIMIT 3;

-- Check relationships (JSONB fields contain linked record IDs)
SELECT
    a.id_field as artwork_id,
    a.title,
    jsonb_array_length(a.type_id) as num_types,
    jsonb_array_length(a.photo_id) as num_photos,
    jsonb_array_length(a.artwork_image_id) as num_images
FROM artwork a
WHERE a.type_id IS NOT NULL
   OR a.photo_id IS NOT NULL
   OR a.artwork_image_id IS NOT NULL
LIMIT 10;

-- Latest sync times
SELECT
    'artwork' as table_name,
    MAX(synced_at) as last_sync
FROM artwork
UNION ALL
SELECT
    'artwork_image',
    MAX(synced_at)
FROM artwork_image
UNION ALL
SELECT
    'archive',
    MAX(synced_at)
FROM archive
UNION ALL
SELECT
    'archive_image',
    MAX(synced_at)
FROM archive_image
UNION ALL
SELECT
    'sold',
    MAX(synced_at)
FROM sold
UNION ALL
SELECT
    'artwork_type',
    MAX(synced_at)
FROM artwork_type
UNION ALL
SELECT
    'photo',
    MAX(synced_at)
FROM photo
UNION ALL
SELECT
    'photo_catagory',
    MAX(synced_at)
FROM photo_catagory
ORDER BY table_name;

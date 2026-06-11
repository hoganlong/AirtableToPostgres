# Example Queries for Keith Long Archive

Now that all your Airtable data is in PostgreSQL with typed columns, here are some useful queries to explore your artwork archive.

## Table Overview

```sql
-- See all tables and their record counts
SELECT
    schemaname,
    tablename,
    n_tup_ins as total_inserts,
    n_live_tup as live_rows
FROM pg_stat_user_tables
WHERE tablename IN ('artwork', 'artwork_image', 'archive', 'archive_image',
                    'sold', 'artwork_type', 'photo', 'photo_catagory')
ORDER BY tablename;
```

## ARTWORK Queries

### Browse Artworks
```sql
-- View all artworks with basic info
SELECT
    id_field,
    title,
    series,
    create_dt,
    medium,
    dimensions,
    location
FROM artwork
WHERE title IS NOT NULL
ORDER BY create_dt DESC
LIMIT 20;
```

### Artworks by Location
```sql
-- Count artworks by location
SELECT
    location,
    COUNT(*) as artwork_count
FROM artwork
WHERE location IS NOT NULL
GROUP BY location
ORDER BY artwork_count DESC;
```

### Artworks by Series
```sql
-- Group artworks by series
SELECT
    series,
    COUNT(*) as count,
    MIN(create_dt) as earliest,
    MAX(create_dt) as latest
FROM artwork
WHERE series IS NOT NULL
GROUP BY series
ORDER BY count DESC;
```

### Artworks by Creation Year
```sql
-- Artworks created per year
SELECT
    EXTRACT(YEAR FROM create_dt) as year,
    COUNT(*) as artwork_count
FROM artwork
WHERE create_dt IS NOT NULL
GROUP BY year
ORDER BY year DESC;
```

### Search by Title or Medium
```sql
-- Search for specific artworks
SELECT
    id_field,
    title,
    medium,
    create_dt
FROM artwork
WHERE
    title ILIKE '%garden%'
    OR medium ILIKE '%watercolor%'
ORDER BY create_dt DESC;
```

## ARTWORK_IMAGE Queries

### Images per Artwork
```sql
-- Count images per artwork using JSONB
SELECT
    ai.id_field,
    ai.photographer,
    ai.view,
    ai.create_dt,
    jsonb_array_length(ai.artwork_id) as num_artworks
FROM artwork_image ai
WHERE ai.artwork_id IS NOT NULL
ORDER BY jsonb_array_length(ai.artwork_id) DESC
LIMIT 20;
```

### Images by Photographer
```sql
-- Count images by photographer
SELECT
    photographer,
    COUNT(*) as image_count
FROM artwork_image
WHERE photographer IS NOT NULL
GROUP BY photographer
ORDER BY image_count DESC;
```

## SOLD Artworks

### Sold Artwork Summary
```sql
-- All sold artworks with prices
SELECT
    id_field,
    collection,
    price,
    sale_dt,
    location,
    notes
FROM sold
ORDER BY sale_dt DESC;
```

### Sales by Year
```sql
-- Total sales by year
SELECT
    EXTRACT(YEAR FROM sale_dt) as year,
    COUNT(*) as items_sold,
    SUM(price) as total_revenue,
    AVG(price) as avg_price
FROM sold
WHERE sale_dt IS NOT NULL AND price IS NOT NULL
GROUP BY year
ORDER BY year DESC;
```

### Sales by Collection
```sql
-- Group sales by collection
SELECT
    collection,
    COUNT(*) as items_sold,
    SUM(price) as total_value
FROM sold
WHERE collection IS NOT NULL
GROUP BY collection
ORDER BY total_value DESC;
```

## PHOTO Queries

### Photos by Category
```sql
-- Photos grouped by category
SELECT
    p.catagory,
    COUNT(*) as photo_count
FROM photo p
GROUP BY p.catagory
ORDER BY photo_count DESC;
```

### Photos by Year
```sql
-- Photos by year
SELECT
    year,
    COUNT(*) as photo_count
FROM photo
WHERE year IS NOT NULL
GROUP BY year
ORDER BY year DESC;
```

### Search Photos
```sql
-- Search photos by description or location
SELECT
    id_field,
    description,
    year,
    location,
    people
FROM photo
WHERE
    description ILIKE '%exhibition%'
    OR location ILIKE '%gallery%'
ORDER BY year DESC;
```

## Cross-Table Analysis

### Artworks with Multiple Types
```sql
-- Find artworks linked to multiple types (using JSONB)
SELECT
    id_field,
    title,
    jsonb_array_length(type_id) as num_types,
    type_id
FROM artwork
WHERE type_id IS NOT NULL
  AND jsonb_array_length(type_id) > 1
ORDER BY jsonb_array_length(type_id) DESC;
```

### Artworks with Many Images
```sql
-- Artworks with the most images
SELECT
    id_field,
    title,
    series,
    jsonb_array_length(artwork_image_id) as image_count
FROM artwork
WHERE artwork_image_id IS NOT NULL
  AND jsonb_array_length(artwork_image_id) > 0
ORDER BY jsonb_array_length(artwork_image_id) DESC
LIMIT 20;
```

### Artwork Image Details
```sql
-- Get detailed image info for specific artworks
-- (This requires extracting IDs from JSONB and joining)
SELECT
    a.id_field as artwork_id,
    a.title,
    ai.id_field as image_id,
    ai.photographer,
    ai.view,
    ai.create_dt
FROM artwork a
JOIN artwork_image ai ON a.artwork_image_id ? ai.airtable_id
WHERE a.id_field = 100  -- Replace with specific artwork ID
ORDER BY ai.create_dt;
```

### Complete Artwork Information
```sql
-- Full details for a specific artwork
SELECT
    a.id_field,
    a.title,
    a.series,
    a.create_dt,
    a.medium,
    a.dimensions,
    a.location,
    a.notes,
    a.type_number,
    a.human_readable_id,
    jsonb_array_length(a.type_id) as num_types,
    jsonb_array_length(a.artwork_image_id) as num_images,
    jsonb_array_length(a.photo_id) as num_photos
FROM artwork a
WHERE a.id_field = 100;  -- Replace with specific artwork ID
```

## Data Quality Checks

### Missing Required Fields
```sql
-- Artworks without titles
SELECT COUNT(*) as no_title_count
FROM artwork
WHERE title IS NULL OR title = '';

-- Artworks without creation dates
SELECT COUNT(*) as no_date_count
FROM artwork
WHERE create_dt IS NULL;
```

### Recently Added/Updated
```sql
-- Recently synced records
SELECT
    'artwork' as table_name,
    COUNT(*) as count,
    MAX(synced_at) as last_sync
FROM artwork
WHERE synced_at > NOW() - INTERVAL '1 day'
UNION ALL
SELECT
    'artwork_image',
    COUNT(*),
    MAX(synced_at)
FROM artwork_image
WHERE synced_at > NOW() - INTERVAL '1 day'
UNION ALL
SELECT
    'photo',
    COUNT(*),
    MAX(synced_at)
FROM photo
WHERE synced_at > NOW() - INTERVAL '1 day';
```

## JSONB Field Queries

### Extract Specific Record IDs from JSONB
```sql
-- Get first type ID for each artwork (if exists)
SELECT
    id_field,
    title,
    type_id->0 as first_type_id
FROM artwork
WHERE type_id IS NOT NULL
  AND jsonb_array_length(type_id) > 0
LIMIT 10;
```

### Expand JSONB Arrays
```sql
-- Expand all type IDs for artworks
SELECT
    id_field,
    title,
    jsonb_array_elements_text(type_id) as type_record_id
FROM artwork
WHERE type_id IS NOT NULL
LIMIT 20;
```

### Search within JSONB
```sql
-- Find artworks linked to a specific type record
SELECT
    id_field,
    title,
    type_id
FROM artwork
WHERE type_id ? 'rec123';  -- Replace with actual Airtable record ID
```

## Export Queries

### CSV Export Format
```sql
-- Prepare artwork data for CSV export
SELECT
    id_field as "ID",
    title as "Title",
    series as "Series",
    TO_CHAR(create_dt, 'YYYY-MM-DD') as "Creation Date",
    medium as "Medium",
    dimensions as "Dimensions",
    location as "Location",
    human_readable_id as "Catalog Number"
FROM artwork
ORDER BY id_field;
```

## Maintenance Queries

### Table Sizes
```sql
-- Check size of each table
SELECT
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE tablename IN ('artwork', 'artwork_image', 'archive', 'archive_image',
                    'sold', 'artwork_type', 'photo', 'photo_catagory')
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

### Index Usage
```sql
-- Check if indexes are being used
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan as index_scans,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
  AND tablename LIKE 'artwork%'
ORDER BY tablename, indexname;
```

## Tips

1. **Use ILIKE for case-insensitive search**: `WHERE title ILIKE '%pattern%'`
2. **Filter NULL values**: `WHERE field IS NOT NULL`
3. **Date ranges**: `WHERE create_dt BETWEEN '2020-01-01' AND '2023-12-31'`
4. **JSONB operators**:
   - `?` checks if key exists: `type_id ? 'rec123'`
   - `->` gets JSON object: `type_id->0`
   - `jsonb_array_length()` gets array size
   - `jsonb_array_elements()` expands arrays

5. **Performance**: For large queries, add indexes on frequently filtered columns:
   ```sql
   CREATE INDEX idx_artwork_location ON artwork(location);
   CREATE INDEX idx_artwork_create_dt ON artwork(create_dt);
   ```

-- Keith Long Archive - Quick Exploration Queries
-- Run these to get familiar with your data

-- ========================================
-- OVERVIEW
-- ========================================

-- Total counts by table
SELECT 'ARTWORK' as table_name, COUNT(*) as records FROM artwork
UNION ALL SELECT 'ARTWORK_IMAGE', COUNT(*) FROM artwork_image
UNION ALL SELECT 'PHOTO', COUNT(*) FROM photo
UNION ALL SELECT 'SOLD', COUNT(*) FROM sold
UNION ALL SELECT 'ARCHIVE', COUNT(*) FROM archive
ORDER BY records DESC;

-- ========================================
-- ARTWORK HIGHLIGHTS
-- ========================================

-- Recent artworks (by creation date)
SELECT
    id_field as id,
    title,
    series,
    TO_CHAR(create_dt, 'YYYY-MM-DD') as created,
    medium,
    location
FROM artwork
WHERE create_dt IS NOT NULL
ORDER BY create_dt DESC
LIMIT 15;

-- Artworks by location
SELECT
    location,
    COUNT(*) as count
FROM artwork
WHERE location IS NOT NULL
GROUP BY location
ORDER BY count DESC;

-- Artworks by series
SELECT
    series,
    COUNT(*) as count,
    MIN(create_dt) as earliest,
    MAX(create_dt) as latest
FROM artwork
WHERE series IS NOT NULL
GROUP BY series
ORDER BY count DESC
LIMIT 10;

-- Artworks created per year
SELECT
    EXTRACT(YEAR FROM create_dt) as year,
    COUNT(*) as artworks_created
FROM artwork
WHERE create_dt IS NOT NULL
GROUP BY year
ORDER BY year DESC;

-- ========================================
-- SALES ANALYSIS
-- ========================================

-- All sold items with details
SELECT
    id_field as id,
    collection as buyer,
    price,
    TO_CHAR(sale_dt, 'YYYY-MM-DD') as sale_date,
    location
FROM sold
ORDER BY sale_dt DESC;

-- Sales summary
SELECT
    COUNT(*) as items_sold,
    TO_CHAR(SUM(price), '$999,999.99') as total_revenue,
    TO_CHAR(AVG(price), '$999,999.99') as avg_price,
    TO_CHAR(MIN(price), '$999,999.99') as lowest_sale,
    TO_CHAR(MAX(price), '$999,999.99') as highest_sale
FROM sold
WHERE price IS NOT NULL;

-- Sales by year
SELECT
    EXTRACT(YEAR FROM sale_dt) as year,
    COUNT(*) as items_sold,
    TO_CHAR(SUM(price), '$999,999.99') as revenue
FROM sold
WHERE sale_dt IS NOT NULL AND price IS NOT NULL
GROUP BY year
ORDER BY year DESC;

-- ========================================
-- IMAGES & PHOTOGRAPHY
-- ========================================

-- Images by photographer
SELECT
    photographer,
    COUNT(*) as images
FROM artwork_image
WHERE photographer IS NOT NULL
GROUP BY photographer
ORDER BY images DESC;

-- Photos by year
SELECT
    year,
    COUNT(*) as photos
FROM photo
WHERE year IS NOT NULL
GROUP BY year
ORDER BY year DESC
LIMIT 10;

-- ========================================
-- RELATIONSHIPS
-- ========================================

-- Artworks with most images
SELECT
    id_field as id,
    title,
    jsonb_array_length(artwork_image_id) as num_images
FROM artwork
WHERE artwork_image_id IS NOT NULL
  AND jsonb_array_length(artwork_image_id) > 0
ORDER BY jsonb_array_length(artwork_image_id) DESC
LIMIT 10;

-- Artworks with types
SELECT
    id_field as id,
    title,
    jsonb_array_length(type_id) as num_types
FROM artwork
WHERE type_id IS NOT NULL
  AND jsonb_array_length(type_id) > 0
ORDER BY id_field
LIMIT 10;

-- ========================================
-- SEARCH EXAMPLES
-- ========================================

-- Search artworks by title (change 'garden' to your search term)
-- SELECT
--     id_field,
--     title,
--     series,
--     medium
-- FROM artwork
-- WHERE title ILIKE '%garden%'
-- ORDER BY create_dt DESC;

-- Search by medium (change 'watercolor' to your search term)
-- SELECT
--     id_field,
--     title,
--     medium,
--     dimensions
-- FROM artwork
-- WHERE medium ILIKE '%watercolor%'
-- ORDER BY create_dt DESC;

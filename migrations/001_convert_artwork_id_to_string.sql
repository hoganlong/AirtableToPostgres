-- Migration: Convert artwork_image.artwork_id from JSONB to VARCHAR
-- Date: 2026-02-15
-- Purpose: artwork_id only contains a single string value, so store as VARCHAR instead of JSONB

-- Step 1: Add new column as VARCHAR
ALTER TABLE artwork_image
ADD COLUMN artwork_id_new VARCHAR(255);

-- Step 2: Extract single string value from JSONB array and populate new column
-- JSONB format is typically: ["recXXXXXXXXXXXXX"] or ["123"]
UPDATE artwork_image
SET artwork_id_new = artwork_id->0->>0
WHERE artwork_id IS NOT NULL
  AND jsonb_array_length(artwork_id) > 0;

-- Step 3: Verify the conversion (check if any rows failed)
-- This should return 0 rows if all conversions were successful
SELECT
  id,
  artwork_id as old_jsonb_value,
  artwork_id_new as new_varchar_value
FROM artwork_image
WHERE artwork_id IS NOT NULL
  AND artwork_id_new IS NULL;

-- If the above query returns 0 rows, proceed with the following steps:

-- Step 4: Drop old column
ALTER TABLE artwork_image
DROP COLUMN artwork_id;

-- Step 5: Rename new column to original name
ALTER TABLE artwork_image
RENAME COLUMN artwork_id_new TO artwork_id;

-- Step 6: Add index for performance (optional but recommended)
CREATE INDEX idx_artwork_image_artwork_id ON artwork_image(artwork_id);

-- Verification query: Show sample of converted data
SELECT id, artwork_id, airtable_id
FROM artwork_image
LIMIT 10;

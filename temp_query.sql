-- Recent artworks
SELECT id_field as id, title, series,
       TO_CHAR(create_dt, 'YYYY-MM-DD') as created,
       medium, location
FROM artwork
WHERE create_dt IS NOT NULL
ORDER BY create_dt DESC
LIMIT 10;

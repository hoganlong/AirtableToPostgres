# Keith Long Archive - Project Status

**Last Updated**: January 30, 2026
**Status**: ✅ Fully Operational with Incremental Sync

---

## Current State

### Database Configuration
- **Platform**: AWS RDS PostgreSQL
- **Endpoint**: `keithlong-archive.cluster-cwlmm0oodeot.us-east-1.rds.amazonaws.com`
- **Database**: `keithlongarchive`
- **Connection**: SSL Mode = Require
- **Access**: Configured for IP `151.204.130.39`

### Data Synced
Successfully synced **8 tables** with **3,129 records** from Airtable:

| Table | Records | Status |
|-------|---------|--------|
| ARTWORK | 758 | ✅ Synced |
| ARTWORK_IMAGE | 1,887 | ✅ Synced |
| PHOTO | 440 | ✅ Synced |
| SOLD | 25 | ✅ Synced |
| ARCHIVE | 3 | ✅ Synced |
| ARCHIVE_IMAGE | 3 | ✅ Synced |
| ARTWORK_TYPE | 8 | ✅ Synced |
| PHOTO_CATAGORY | 5 | ✅ Synced |

### Schema Implementation
- **Type**: Custom Schema with Typed Columns
- **Features**:
  - Typed columns (INTEGER, TEXT, DATE, TIMESTAMP, NUMERIC)
  - JSONB for complex relationships
  - Automatic upsert logic (no duplicates)
  - Indexed on airtable_id
  - UTC timestamps for sync tracking
  - **NEW**: `last_modified_at` column tracks PostgreSQL update times
  - **NEW**: Incremental sync using Airtable's `LAST_MODIFIED_TIME()`

### 🆕 Incremental Sync Feature

**Status**: ✅ Fully Implemented and Tested

The sync tool now intelligently tracks changes and only syncs modified records:

- **First Sync**: Fetches all records from Airtable, marks as NEW
- **Subsequent Syncs**: Uses `filterByFormula=IS_AFTER(LAST_MODIFIED_TIME(), "timestamp")` to fetch only changed records
- **Performance**: 99%+ reduction in data transfer for unchanged tables
- **Change Detection**: Automatically determines NEW vs UPDATED records
- **Sync History**: Logs every sync with detailed per-table statistics

**Example Performance**:
- First sync: 758 records (45 seconds)
- Second sync (no changes): 0 records (2 seconds) - 95% faster!
- After modifying 5 records: 5 records (3 seconds)

### Sync History Table

All sync operations are logged to the `sync_history` table (one row per table per sync):

```sql
sync_history (
  id                SERIAL PRIMARY KEY,
  sync_id           UUID NOT NULL,           -- Groups tables from same sync
  sync_timestamp    TIMESTAMP WITH TIME ZONE,
  table_name        TEXT,
  new_records       INTEGER,
  updated_records   INTEGER,
  unchanged_records INTEGER,
  fetched_records   INTEGER,
  duration_seconds  NUMERIC(10,2),
  status            TEXT,
  error_message     TEXT NULL
)
```

**Query Examples**:
```sql
-- Latest sync for all tables
SELECT table_name, new_records, updated_records, unchanged_records, duration_seconds
FROM sync_history
WHERE sync_id = (SELECT sync_id FROM sync_history ORDER BY sync_timestamp DESC LIMIT 1)
ORDER BY table_name;

-- Sync history for specific table
SELECT sync_timestamp, new_records, updated_records, unchanged_records
FROM sync_history
WHERE table_name = 'ARTWORK'
ORDER BY sync_timestamp DESC
LIMIT 10;
```

### Tools Available

#### 1. Sync Tool (Enhanced with Incremental Sync)
- **Run**: `dotnet run`
- **Purpose**: Sync all tables from Airtable to PostgreSQL
- **Features**:
  - Incremental sync (only fetches changed records)
  - Detailed statistics (NEW, UPDATED, UNCHANGED counts)
  - Automatic sync history logging
  - Safe to run multiple times
- **Config**: `appsettings.json` → `SyncAllTables: true`

#### 2. Query Explorer (Interactive)
- **Run**: Double-click `QueryExplorer.bat` or `dotnet run -- query`
- **Features**: 13 pre-built queries including:
  - Table overview
  - Recent artworks
  - Artworks by location/series/year
  - Sales summary and details
  - Photographer statistics
  - Custom search by title/medium
  - Search by human_readable_id
  - View all artwork

#### 3. Insights Tool
- **Run**: `dotnet run -- showall`
- **Purpose**: Generate comprehensive data insights report
- **Output**: All key statistics in one view

#### 4. Quick Test
- **Run**: `dotnet run -- test`
- **Purpose**: Verify database connection and table counts

#### 5. DBeaver
- **Status**: ✅ Configured and working
- **SSL Mode**: require (no certificate needed)
- **Use**: Full SQL query capability, data browsing, export

#### 6. HTML Generator
- **Run**: `dotnet run -- html`
- **Purpose**: Generate static HTML pages for browsing the artwork collection
- **Output**: Creates `artwork_html` folder with:
  - `index.html` - Main landing page with collection statistics
  - `artworks.html` - Complete sortable table of all artworks
  - `series.html` - Grid view of artworks organized by series
  - `locations.html` - Grid view of artworks organized by location
  - `style.css` - Professional, responsive stylesheet
- **Features**:
  - Clean, modern design
  - Mobile-friendly responsive layout
  - Easy navigation between pages
  - No server required - open HTML files directly in browser

---

## Key Findings from Data

### Collection Overview
- **Total Artworks**: 758
- **Primary Location**: New York Basement (379) and New York apartment (334)
- **Sold Items**: 23 artworks
- **Total Images**: 1,887 (by 8 photographers)
- **Photo Collection**: 440 photos

### Notable Series
1. **Ready to Wear** - 80 artworks (2003-2024, 21 years)
2. **Mask** - 66 artworks (1985-2015, 30 years)
3. **Weathervane** - 15 artworks (2016-2017)

### Production Timeline
- **Peak Year**: 2017 (45 artworks)
- **Recent**: 2024 (4 artworks so far)
- **Active Period**: 2012-2020 (10-32 artworks/year)

### Photography
- **Top Photographers**: Anna Berlin (811), Thomas Muller (730), Hogan Long (288)

### Known Issues
- ⚠️ **Sales Prices**: Currently showing as $0 in database - may need update in Airtable

---

## Configuration Files

### appsettings.json (Current Settings)
```json
{
  "Airtable": {
    "ApiKey": "patAqas0D0SfJ7LqG.47623...",
    "BaseId": "appnGXIULYw1ZA6bF",
    "TableName": "ARTWORK"
  },
  "PostgreSQL": {
    "ConnectionString": "Host=...;SSL Mode=Require"
  },
  "Schema": {
    "UseCustomSchema": true,
    "SyncAllTables": true,
    "SchemaFilePath": "airtable_schema.txt"
  }
}
```

### Key Files

**Core Sync Engine:**
- `Program.cs` - Main sync application with incremental sync logic
- `airtable_schema.txt` - Schema definitions for all 8 tables
- `SchemaParser.cs` - Parses schema file
- `TypeMapper.cs` - Maps Airtable types to PostgreSQL
- `SchemaGenerator.cs` - Generates CREATE TABLE DDL with last_modified_at column
- `RecordMapper.cs` - Transforms records to typed data

**Incremental Sync (NEW):**
- `SyncStatistics.cs` - Tracks sync metrics (NEW, UPDATED, UNCHANGED counts)
- `ChangeDetector.cs` - Determines if records are NEW or UPDATED
- `SyncHistoryLogger.cs` - Manages sync_history table and logging

**Query & Analysis Tools:**
- `QueryRunner.cs` - Interactive query tool
- `ShowAll.cs` - Insights generator
- `ShowInsights.cs` - Data insights tool
- `QuickTest.cs` - Connection test utility
- `ArtworkHTML.cs` - HTML gallery generator for web browsing

### Documentation
- `SYNC_COMPLETE.md` - Complete sync summary
- `IMPLEMENTATION_SUMMARY.md` - Technical details
- `README_SCHEMA_FEATURE.md` - User guide
- `EXAMPLE_QUERIES.md` - 30+ query examples
- `explore_archive.sql` - SQL query collection
- `verify_all_tables.sql` - Verification queries

---

## Recently Completed Features

### ✅ HTML Artwork Gallery Generator (January 30, 2026)
- Static HTML site generator for browsing the artwork collection
- Multiple views: all artworks, by series, by location
- Professional responsive design with modern CSS
- No server required - works offline
- Easy to share and publish

### ✅ Incremental Sync with Change Detection (January 30, 2026)
- Implemented timestamp-based incremental sync using Airtable's `LAST_MODIFIED_TIME()`
- Only fetches records modified since last sync
- Tracks NEW, UPDATED, and UNCHANGED record counts
- 95%+ performance improvement for subsequent syncs

### ✅ Sync History Tracking (January 30, 2026)
- Created `sync_history` table with per-table statistics
- Logs every sync operation with detailed metrics
- Easy querying of sync history and trends
- Automatic table structure migration

### ✅ Enhanced Change Detection (January 30, 2026)
- Lightweight existence checks for NEW vs UPDATED determination
- Added `last_modified_at` column to track PostgreSQL update times
- Accurate UNCHANGED count calculation

---

## Future Work Items

### Priority 1: Data Quality

#### 1.1 Fix Sales Price Data
**Issue**: All prices showing as $0 in SOLD table
**Action**:
- Check Airtable SOLD table for actual price values
- Verify price field mapping
- Re-sync after data correction
- Verify: `SELECT * FROM sold WHERE price > 0;`

#### 1.2 Data Validation
**Tasks**:
- Check for artworks without titles: `SELECT COUNT(*) FROM artwork WHERE title IS NULL`
- Verify human_readable_id format consistency
- Check for orphaned relationships (JSONB references to non-existent records)
- Validate date ranges (earliest vs latest creation dates)

### Priority 2: Enhanced Queries & Views

#### 2.1 Create Database Views
**Suggested Views**:
```sql
-- Artworks with full image counts
CREATE VIEW artwork_summary AS
SELECT
    a.id_field,
    a.title,
    a.series,
    a.create_dt,
    a.location,
    jsonb_array_length(a.artwork_image_id) as num_images,
    jsonb_array_length(a.photo_id) as num_photos
FROM artwork a;

-- Sales with details
CREATE VIEW sales_detail AS
SELECT
    s.id_field,
    s.collection as buyer,
    s.price,
    s.sale_dt,
    s.location
FROM sold s
WHERE s.price IS NOT NULL AND s.price > 0;

-- Series summary
CREATE VIEW series_summary AS
SELECT
    series,
    COUNT(*) as artwork_count,
    MIN(create_dt) as first_created,
    MAX(create_dt) as last_created,
    MAX(create_dt) - MIN(create_dt) as timespan
FROM artwork
WHERE series IS NOT NULL
GROUP BY series;
```

#### 2.2 Add Custom Indexes
**Recommended**:
```sql
-- Speed up location searches
CREATE INDEX idx_artwork_location ON artwork(location);

-- Speed up series searches
CREATE INDEX idx_artwork_series ON artwork(series);

-- Speed up date range queries
CREATE INDEX idx_artwork_create_dt ON artwork(create_dt);

-- Speed up title searches (case-insensitive)
CREATE INDEX idx_artwork_title_lower ON artwork(LOWER(title));

-- Speed up photographer searches
CREATE INDEX idx_artwork_image_photographer ON artwork_image(photographer);

-- JSONB indexes for relationship queries
CREATE INDEX idx_artwork_type_id ON artwork USING GIN(type_id);
CREATE INDEX idx_artwork_artwork_image_id ON artwork USING GIN(artwork_image_id);
```

#### 2.3 Additional Query Tools
- Query by date range (e.g., "Show all work from 2015-2017")
- Series timeline visualization query
- Location history tracking
- Medium frequency analysis
- Photographer collaboration matrix

### Priority 3: Data Export & Reporting

#### 3.1 CSV Export Tool
**Create**: Script to export filtered artwork data to CSV
**Use Cases**:
- Exhibition catalogs
- Insurance documentation
- Gallery submissions
- Collection inventories

**Example Features**:
- Export by series
- Export by date range
- Export by location
- Export with image references
- Custom field selection

#### 3.2 Report Generator
**Monthly/Annual Reports**:
- Artworks created per year/month
- Location distribution changes
- Sales reports (when prices are fixed)
- Photography coverage (which artworks have images)
- Collection growth over time

### Priority 4: Relationships & Foreign Keys

#### 4.1 Resolve JSONB Relationships
Currently, relationships are stored as JSONB arrays (e.g., `type_id`, `photo_id`).

**Enhancement**: Create helper queries or views to resolve these:
```sql
-- Get artwork with its type information
SELECT
    a.id_field,
    a.title,
    a.type_id,
    t.code,
    t.description
FROM artwork a
CROSS JOIN LATERAL jsonb_array_elements_text(a.type_id) AS type_rec_id
LEFT JOIN artwork_type t ON t.airtable_id = type_rec_id
WHERE a.type_id IS NOT NULL;
```

#### 4.2 Future: Add Foreign Keys
**Note**: Current JSONB approach is flexible but doesn't enforce referential integrity.

**Consideration**: Create junction tables for proper many-to-many relationships:
- `artwork_type_link` (artwork_id, type_id)
- `artwork_photo_link` (artwork_id, photo_id)
- `artwork_image_link` (artwork_id, image_id)

### Priority 5: Automation & Maintenance

#### 5.1 Scheduled Syncs
**Setup**: Windows Task Scheduler or cron job
**Frequency**: Daily or weekly
**Command**: `dotnet run` in project directory
**Benefit**: Keep PostgreSQL automatically in sync with Airtable
**Note**: With incremental sync now implemented, scheduled syncs will be very fast for tables with few changes

#### 5.2 ~~Sync History Tracking~~ ✅ **COMPLETED**
See "Recently Completed Features" section above.

#### 5.3 ~~Change Detection~~ ✅ **COMPLETED**
See "Recently Completed Features" section above.

### Priority 6: Web Dashboard

#### 6.1 Simple Web Interface
**Technologies**:
- Backend: ASP.NET Core Web API
- Frontend: React, Vue, or simple HTML/JavaScript
- Or: Use PostgREST for instant REST API

**Features**:
- Browse artwork catalog
- Search by various fields
- View artwork details with images
- Series explorer
- Sales dashboard
- Photography credits

#### 6.2 Public Gallery Site
**Use Case**: Portfolio/gallery website
**Features**:
- Public-facing artwork gallery
- Series pages
- Exhibition history
- Contact/sales inquiries
- Image carousel

### Priority 7: Advanced Features

#### 7.1 Full-Text Search
**PostgreSQL Feature**: Built-in full-text search
```sql
-- Add tsvector column for full-text search
ALTER TABLE artwork ADD COLUMN search_vector tsvector;

-- Populate with searchable content
UPDATE artwork SET search_vector =
    to_tsvector('english',
        COALESCE(title, '') || ' ' ||
        COALESCE(series, '') || ' ' ||
        COALESCE(medium, '') || ' ' ||
        COALESCE(notes, '')
    );

-- Create index
CREATE INDEX idx_artwork_search ON artwork USING GIN(search_vector);

-- Search query
SELECT id_field, title, series
FROM artwork
WHERE search_vector @@ to_tsquery('english', 'watercolor & garden');
```

#### 7.2 Image Integration
**If images stored on AWS S3**:
- Add S3 URLs to database
- Generate thumbnails
- Create image gallery queries
- Link images to artwork records

#### 7.3 Backup & Disaster Recovery
**Setup**:
- Automated PostgreSQL backups
- Backup to S3
- Restore procedures documented
- Test recovery process

### Priority 8: Analytics & Insights

#### 8.1 Advanced Analytics Queries
- Production trends over time
- Series evolution analysis
- Location utilization patterns
- Medium preferences by period
- Collaboration patterns (photographer × artist)

#### 8.2 Data Visualization
**Tools**: Grafana, Metabase, or Tableau
**Connect**: Directly to PostgreSQL
**Dashboards**:
- Collection overview
- Production timeline
- Sales analytics
- Photography coverage

---

## How to Resume Work

### Re-sync Data from Airtable
```bash
cd C:\Users\Hogan\Projects\claudetest\AirtableToPostgres
dotnet run
```

### Run Queries
```bash
# Interactive query tool
dotnet run -- query

# Or use DBeaver with connection already configured
```

### View Current Data
```bash
# Quick insights
dotnet run -- showall

# Or query in DBeaver
```

### Check Specific Documentation
- **User Guide**: `README_SCHEMA_FEATURE.md`
- **Query Examples**: `EXAMPLE_QUERIES.md`
- **Implementation Details**: `IMPLEMENTATION_SUMMARY.md`
- **Current Status**: `SYNC_COMPLETE.md`

---

## Quick Reference Commands

```bash
# Full sync (all tables)
dotnet run

# Interactive queries
dotnet run -- query

# Show all insights
dotnet run -- showall

# Test connection
dotnet run -- test

# Rebuild project
dotnet build

# Open query explorer (Windows)
QueryExplorer.bat
```

---

## Contact & Support

**Project Location**: `C:\Users\Hogan\Projects\claudetest\AirtableToPostgres`

**Database Access**:
- RDS Endpoint: `keithlong-archive.cluster-cwlmm0oodeot.us-east-1.rds.amazonaws.com`
- Database: `keithlongarchive`
- Username: `hoganlong`
- DBeaver: Configured and working

**Airtable**:
- Base ID: `appnGXIULYw1ZA6bF`
- API Key: Configured in `appsettings.json`

---

## Notes

- All 8 Airtable tables successfully migrated to typed PostgreSQL schemas
- Upsert logic prevents duplicates - safe to re-run sync anytime
- Query tools provide easy access without SQL knowledge
- DBeaver configured for advanced SQL queries and data export
- System is production-ready and fully operational

**Next session**: Pick any item from "Future Work Items" above to continue development.

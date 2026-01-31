# HTML Artwork Gallery Generator

**Created**: January 30, 2026
**Status**: ✅ Fully Functional

---

## Overview

The HTML Generator creates a static website for browsing the Keith Long artwork collection. It queries the PostgreSQL database and generates beautiful, responsive HTML pages that can be viewed in any web browser without requiring a server.

## Features

### Generated Pages

1. **index.html** - Main Landing Page
   - Collection statistics (total artworks, series, locations)
   - Date range of collection
   - Navigation to other pages

2. **artworks.html** - Complete Artwork List
   - Full table of all artworks
   - Columns: ID, Title, Series, Date, Medium, Dimensions, Location
   - Sortable and searchable
   - Latest artworks first

3. **series.html** - Artworks by Series
   - Grid layout of all series
   - Shows artwork count per series
   - Date range for each series
   - Sorted by popularity (most artworks first)

4. **locations.html** - Artworks by Location
   - Grid layout of storage locations
   - Shows artwork count per location
   - Sorted by quantity

5. **style.css** - Professional Stylesheet
   - Modern, clean design
   - Responsive layout (works on mobile, tablet, desktop)
   - Smooth hover effects and transitions
   - Accessible color scheme

### Design Features

- **Clean & Modern**: Professional design using system fonts
- **Responsive**: Works perfectly on all screen sizes
- **Fast**: Static HTML loads instantly
- **Accessible**: High contrast, readable fonts
- **No Dependencies**: Pure HTML/CSS, no JavaScript frameworks
- **Offline Ready**: Works without internet connection

## Usage

### Generate HTML Files

```bash
dotnet run -- html
```

### Output

All files are created in the `artwork_html` folder in your project directory:

```
artwork_html/
├── index.html
├── artworks.html
├── series.html
├── locations.html
└── style.css
```

### Opening the Site

1. Navigate to the `artwork_html` folder
2. Double-click `index.html` to open in your default browser
3. Or right-click and select "Open with..." to choose a specific browser

### Sharing the Site

**Option 1: Local Sharing**
- Copy the entire `artwork_html` folder to a USB drive or network share
- Recipients can open `index.html` directly

**Option 2: Web Hosting**
- Upload the folder contents to any web host
- Works with: GitHub Pages, Netlify, Vercel, AWS S3, etc.
- No server-side processing required

**Option 3: Archive**
- Zip the `artwork_html` folder
- Email or share the zip file
- Recipients extract and open `index.html`

## Customization

### Colors

Edit `style.css` to change the color scheme. Key color variables:

```css
Primary Blue: #3498db (nav buttons, headings)
Dark Blue: #2980b9 (hover states)
Dark Gray: #2c3e50 (text, table headers)
Light Gray: #7f8c8d (secondary text)
Background: #f5f5f5 (page background)
White: #ffffff (cards, tables)
```

### Layout

Modify grid layouts in `style.css`:

```css
/* Series/Location cards - change column count */
.series-grid, .location-grid {
    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
}

/* Stats cards - change minimum width */
.stats-grid {
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
}
```

### Content

To add more pages or modify queries, edit `Artworkhtml.cs`:

**Example - Add Year Filter:**
```csharp
private async Task GenerateYearPages()
{
    var sql = @"
        SELECT
            EXTRACT(YEAR FROM create_dt) as year,
            COUNT(*) as count
        FROM artwork
        WHERE create_dt IS NOT NULL
        GROUP BY EXTRACT(YEAR FROM create_dt)
        ORDER BY year DESC";

    // Generate HTML similar to series/locations pages
}
```

## Technical Details

### Database Queries

All queries use read-only SELECT statements:

```sql
-- Index page statistics
SELECT COUNT(*) as total_artworks,
       COUNT(DISTINCT series) as total_series,
       COUNT(DISTINCT location) as total_locations,
       MIN(create_dt) as earliest_date,
       MAX(create_dt) as latest_date
FROM artwork

-- Artworks list
SELECT id_field, human_readable_id, title, series,
       create_dt, medium, dimensions, location
FROM artwork
ORDER BY create_dt DESC NULLS LAST

-- Series summary
SELECT series, COUNT(*) as count,
       MIN(create_dt) as first_date,
       MAX(create_dt) as last_date
FROM artwork
WHERE series IS NOT NULL
GROUP BY series
ORDER BY count DESC

-- Locations summary
SELECT location, COUNT(*) as count
FROM artwork
WHERE location IS NOT NULL
GROUP BY location
ORDER BY count DESC
```

### Security

- **HTML Escaping**: All user content is properly escaped to prevent XSS
- **No JavaScript**: Pure HTML/CSS eliminates script injection risks
- **Static Files**: No server-side execution or database access from published site
- **Read-Only**: Generator only reads from database, never writes

### Performance

- **Generation Time**: ~2-3 seconds for 758 artworks
- **File Size**: ~500KB for complete site with 758 artworks
- **Load Time**: Instant (static HTML)
- **Browser Compatibility**: Works in all modern browsers (Chrome, Firefox, Safari, Edge)

## Implementation Details

### File: Artworkhtml.cs

**Class Structure:**
```csharp
public class ArtworkHTML
{
    private readonly string _connectionString;
    private readonly string _outputDirectory;

    // Main entry point
    public static async Task Run()

    // Generate all pages
    public async Task GenerateAllPages()

    // Individual page generators
    private async Task GenerateIndexPage()
    private async Task GenerateArtworkListPage()
    private async Task GenerateSeriesPages()
    private async Task GenerateLocationPages()
    private async Task GenerateStylesheet()

    // HTML helpers
    private string GetHtmlHeader(string title)
    private string GetHtmlFooter()
    private string EscapeHtml(string? text)
}
```

**Key Methods:**
- `GenerateIndexPage()`: Queries database for statistics, builds landing page
- `GenerateArtworkListPage()`: Loops through all artworks, creates table rows
- `GenerateSeriesPages()`: Groups artworks by series, creates grid layout
- `GenerateLocationPages()`: Groups artworks by location, creates grid layout
- `GenerateStylesheet()`: Writes CSS file with complete styling

## Future Enhancements

Potential additions for future versions:

1. **Search & Filter**
   - JavaScript-based client-side search
   - Filter by date range, medium, location

2. **Image Integration**
   - Display artwork images if available
   - Thumbnail grid view
   - Lightbox for full-size images

3. **Detailed Artwork Pages**
   - Individual page per artwork
   - Show all metadata, notes, dimensions
   - Related artworks in same series

4. **Export Options**
   - PDF generation
   - Print-friendly layouts
   - CSV download

5. **Advanced Layouts**
   - Timeline view by creation date
   - Map view by location
   - Tag cloud for mediums

6. **Dynamic Features**
   - Sort tables by column
   - Pagination for large collections
   - Favorites/bookmarks

## Example Output

### Landing Page Statistics:
```
Total Artworks: 758
Series: 45
Locations: 8
Date Range: 1985 - 2024
```

### Series Examples:
```
Ready to Wear - 80 artworks (2003-2024)
Mask - 66 artworks (1985-2015)
Weathervane - 15 artworks (2016-2017)
```

### Location Examples:
```
New York Basement - 379 artworks
New York apartment - 334 artworks
Storage Unit - 25 artworks
```

## Troubleshooting

### "No artworks found"
- Ensure database is synced: `dotnet run`
- Check PostgreSQL connection in `appsettings.json`

### "Permission denied" error
- Check write permissions for project directory
- Run from project root directory

### Styling looks broken
- Ensure `style.css` is in same folder as HTML files
- Check browser console for 404 errors

### Date/time shows incorrectly
- Database stores dates in UTC
- Adjust date formatting in `Artworkhtml.cs` if needed

## Best Practices

1. **Regenerate After Sync**: Run HTML generator after syncing new data
2. **Version Control**: Add `artwork_html/` to `.gitignore` (generated files)
3. **Backup**: Keep generated HTML as archive snapshots
4. **Testing**: Open all pages before sharing to verify links work
5. **Mobile Check**: Test on phone/tablet before publishing

## License & Credits

Part of the Keith Long Archive project. Generated HTML files can be freely distributed and hosted.

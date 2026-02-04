using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text;

namespace AirtableToPostgres;

public class ArtworkHTML
{
    private readonly string _connectionString;
    private readonly string _outputDirectory;

    public ArtworkHTML(string connectionString, string outputDirectory)
    {
        _connectionString = connectionString;
        _outputDirectory = outputDirectory;
    }

    public static async Task Run()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration["PostgreSQL:ConnectionString"];
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artwork_html");

        var generator = new ArtworkHTML(connectionString!, outputDirectory);

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Keith Long Archive - Artwork HTML Generation         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Create output directory
        Directory.CreateDirectory(outputDirectory);
        Console.WriteLine($"Output directory: {outputDirectory}\n");

        // Generate HTML files
        Console.WriteLine("Generating HTML pages...");
        await generator.GenerateAllPages();

        Console.WriteLine($"\n✓ HTML files generated successfully!");
        Console.WriteLine($"✓ Open: {Path.Combine(outputDirectory, "index.html")}");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    public async Task GenerateAllPages()
    {
        await GenerateIndexPage();
        await GenerateArtworkListPage();
        await GenerateSeriesPages();
        await GenerateLocationPages();
        await GenerateStylesheet();

        Console.WriteLine("  ✓ index.html - Main landing page");
        Console.WriteLine("  ✓ artworks.html - Complete artwork list");
        Console.WriteLine("  ✓ series.html - Artworks by series");
        Console.WriteLine("  ✓ locations.html - Artworks by location");
        Console.WriteLine("  ✓ style.css - Stylesheet");
    }

    private async Task GenerateIndexPage()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get summary statistics
        var sql = @"
            SELECT
                COUNT(*) as total_artworks,
                COUNT(DISTINCT series) as total_series,
                COUNT(DISTINCT location) as total_locations,
                MIN(create_dt) as earliest_date,
                MAX(create_dt) as latest_date
            FROM artwork
            WHERE create_dt IS NOT NULL";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        int totalArtworks = 0, totalSeries = 0, totalLocations = 0;
        string? earliestDate = null, latestDate = null;

        if (await reader.ReadAsync())
        {
            totalArtworks = reader.GetInt32(0);
            totalSeries = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            totalLocations = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            earliestDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy");
            latestDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy");
        }

        var html = new StringBuilder();
        html.AppendLine(GetHtmlHeader("Keith Long Archive"));
        html.AppendLine(@"
    <div class='container'>
        <h1>Keith Long Archive</h1>
        <p class='subtitle'>Digital Archive of Artwork Collection</p>

        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-number'>" + totalArtworks + @"</div>
                <div class='stat-label'>Total Artworks</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + totalSeries + @"</div>
                <div class='stat-label'>Series</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + totalLocations + @"</div>
                <div class='stat-label'>Locations</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + earliestDate + " - " + latestDate + @"</div>
                <div class='stat-label'>Date Range</div>
            </div>
        </div>

        <div class='navigation'>
            <a href='artworks.html' class='nav-button'>Browse All Artworks</a>
            <a href='series.html' class='nav-button'>View by Series</a>
            <a href='locations.html' class='nav-button'>View by Location</a>
        </div>
    </div>");
        html.AppendLine(GetHtmlFooter());

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "index.html"), html.ToString());
    }


    private static string BlankOrWithBR(string inS, string prepend="")
    {
      if (!string.IsNullOrEmpty(inS))
      {
        return(prepend+inS+"<br/>");
      }
      else
        return("");
    }
    private async Task GenerateArtworkListPage()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT
                id_field,
                -- reference_image
                iFileName,
                title,
                series,
                create_dt,
                medium,
                dimensions,
                FOLDED_DIMENSIONS,
                location,
                notes,
                human_readable_id,
                artwork_image_id
            FROM artwork
            ORDER BY human_readable_id ASC NULLS LAST";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var html = new StringBuilder();
        html.AppendLine(GetHtmlHeader("All Artworks - Keith Long Archive"));
        /*
        html.AppendLine(@"
    <div class='container'>
        <h1>All Artworks</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>
        */
        html.AppendLine("<div class='gallery'>");
        while (await reader.ReadAsync())
        {
            var id = reader.IsDBNull(0) ? "" : reader.GetInt32(0).ToString();
            var iFileName = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var title = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var series = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var ctDate = reader.IsDBNull(4) ? "" : reader.GetDateTime(4).ToString("yyyy-MM-dd");
            var medium = reader.IsDBNull(5) ? "" : reader.GetString(5);
            var dimensions = reader.IsDBNull(6) ? "" : reader.GetString(6);
            var foldDimensions = reader.IsDBNull(7) ? "" : reader.GetString(7);
            var location = reader.IsDBNull(8) ? "" : reader.GetString(8);
            var notes = reader.IsDBNull(9) ? "" : reader.GetString(9);
            var humanId = reader.IsDBNull(10) ? "" : reader.GetString(10);
            var image_ids = reader.IsDBNull(11) ? "" : reader.GetString(11);

            bool haveImg = !string.IsNullOrEmpty(iFileName);
            var tifURL = haveImg ? "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/"+iFileName+".tif" : "";
            var imgURL = haveImg ? "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/jpg/"+iFileName+".jpg" : "";
                     
            html.AppendLine($@"<div class='gallery-item'>");
            if (haveImg)
            {
              html.AppendLine($@"  <a href='{imgURL}' target='_blank' rel='noopener noreferrer'>
                     <img src='{imgURL}' title='(click for full size)'/>
                    </a><br/>
                    <div class='desc'><a class='desc' href='{tifURL}'>[tif file]</a></div>");
            }
            html.AppendLine($"  <div class='desc'>"); 
            html.AppendLine($"    {BlankOrWithBR(title,"  ")}");
           // html.AppendLine($"    {BlankOrWithBR(series,"  ")}");
            html.AppendLine($"    {BlankOrWithBR(ctDate,"  ")}");
            html.AppendLine($"    {BlankOrWithBR(medium,"  ")}");
            html.AppendLine($"    {BlankOrWithBR(dimensions,"  ")}");
            html.AppendLine($"    {BlankOrWithBR(foldDimensions,"   Folded: ")}");
          //  html.AppendLine($"    {BlankOrWithBR(location,"  Location: ")}");
            html.AppendLine($"    {BlankOrWithBR(notes,"  Notes: ")}");
            html.AppendLine($"    {BlankOrWithBR(humanId,"  ")}");
            html.AppendLine($"  </div>"); 
       //       html.AppendLine($"    {BlankOrWithBR(image_ids,"img: ")}");
            
            html.AppendLine($"</div>  <!-- gallery item -->"); 
        }

        html.AppendLine(@"</div>");
        html.AppendLine(GetHtmlFooter());

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "artworks.html"), html.ToString());
    }


/*
    private async Task GenerateArtworkListPage()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT
                id_field,
                human_readable_id,
                title,
                series,
                create_dt,
                medium,
                dimensions,
                location,
                notes
            FROM artwork
            ORDER BY human_readable_id ASC NULLS LAST, id_field DESC";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var html = new StringBuilder();
        html.AppendLine(GetHtmlHeader("All Artworks - Keith Long Archive"));
        html.AppendLine(@"
    <div class='container'>
        <h1>All Artworks</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <table class='artwork-table'>
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Title</th>
                    <th>Series</th>
                    <th>Date</th>
                    <th>Medium</th>
                    <th>Dimensions</th>
                    <th>Location</th>
                </tr>
            </thead>
            <tbody>");

        while (await reader.ReadAsync())
        {
            var id = reader.IsDBNull(0) ? "" : reader.GetInt32(0).ToString();
            var humanId = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var title = reader.IsDBNull(2) ? "Untitled" : reader.GetString(2);
            var series = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var date = reader.IsDBNull(4) ? "" : reader.GetDateTime(4).ToString("yyyy-MM-dd");
            var medium = reader.IsDBNull(5) ? "" : reader.GetString(5);
            var dimensions = reader.IsDBNull(6) ? "" : reader.GetString(6);
            var location = reader.IsDBNull(7) ? "" : reader.GetString(7);

            html.AppendLine($@"
                <tr>
                    <td class='id-cell'>{humanId}</td>
                    <td class='title-cell'>{EscapeHtml(title)}</td>
                    <td>{EscapeHtml(series)}</td>
                    <td class='date-cell'>{date}</td>
                    <td>{EscapeHtml(medium)}</td>
                    <td class='dimensions-cell'>{EscapeHtml(dimensions)}</td>
                    <td>{EscapeHtml(location)}</td>
                </tr>");
        }

        html.AppendLine(@"
            </tbody>
        </table>
    </div>");
        html.AppendLine(GetHtmlFooter());

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "artworks.html"), html.ToString());
    }
*/
    private async Task GenerateSeriesPages()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get series summary
        var seriesSql = @"
            SELECT
                series,
                COUNT(*) as count,
                MIN(create_dt) as first_date,
                MAX(create_dt) as last_date
            FROM artwork
            WHERE series IS NOT NULL AND series != ''
            GROUP BY series
            ORDER BY count DESC";

        var html = new StringBuilder();
        html.AppendLine(GetHtmlHeader("Artworks by Series - Keith Long Archive"));
        html.AppendLine(@"
    <div class='container'>
        <h1>Artworks by Series</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <div class='series-grid'>");

        await using var cmd = new NpgsqlCommand(seriesSql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var series = reader.GetString(0);
            var count = reader.GetInt32(1);
            var firstDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2).ToString("yyyy");
            var lastDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy");
            var dateRange = (firstDate == lastDate) ? firstDate : $"{firstDate} - {lastDate}";

            html.AppendLine($@"
            <div class='series-card'>
                <h3>{EscapeHtml(series)}</h3>
                <div class='series-count'>{count} artwork{(count != 1 ? "s" : "")}</div>
                <div class='series-date'>{dateRange}</div>
            </div>");
        }

        html.AppendLine(@"
        </div>
    </div>");
        html.AppendLine(GetHtmlFooter());

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "series.html"), html.ToString());
    }

    private async Task GenerateLocationPages()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var locationSql = @"
            SELECT
                location,
                COUNT(*) as count
            FROM artwork
            WHERE location IS NOT NULL AND location != ''
            GROUP BY location
            ORDER BY count DESC";

        var html = new StringBuilder();
        html.AppendLine(GetHtmlHeader("Artworks by Location - Keith Long Archive"));
        html.AppendLine(@"
    <div class='container'>
        <h1>Artworks by Location</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <div class='location-grid'>");

        await using var cmd = new NpgsqlCommand(locationSql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var location = reader.GetString(0);
            var count = reader.GetInt32(1);

            html.AppendLine($@"
            <div class='location-card'>
                <h3>{EscapeHtml(location)}</h3>
                <div class='location-count'>{count} artwork{(count != 1 ? "s" : "")}</div>
            </div>");
        }

        html.AppendLine(@"
        </div>
    </div>");
        html.AppendLine(GetHtmlFooter());

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "locations.html"), html.ToString());
    }

    private async Task GenerateStylesheet()
    {
        var css = @"
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
    line-height: 1.6;
    color: #333;
    background: #f5f5f5;
}

.container {
    max-width: 1400px;
    margin: 0 auto;
    padding: 20px;
}

h1 {
    font-size: 2.5em;
    margin-bottom: 10px;
    color: #2c3e50;
}

.subtitle {
    color: #7f8c8d;
    margin-bottom: 30px;
}

.subtitle a {
    color: #3498db;
    text-decoration: none;
}

.subtitle a:hover {
    text-decoration: underline;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin: 40px 0;
}

.stat-card {
    background: white;
    padding: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    text-align: center;
}

.stat-number {
    font-size: 2.5em;
    font-weight: bold;
    color: #3498db;
    margin-bottom: 10px;
}

.stat-label {
    color: #7f8c8d;
    font-size: 0.9em;
}

.navigation {
    display: flex;
    gap: 15px;
    margin: 40px 0;
    flex-wrap: wrap;
}

.nav-button {
    display: inline-block;
    padding: 15px 30px;
    background: #3498db;
    color: white;
    text-decoration: none;
    border-radius: 5px;
    font-weight: 500;
    transition: background 0.3s;
}

.nav-button:hover {
    background: #2980b9;
}

.artwork-table {
    width: 100%;
    background: white;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.artwork-table thead {
    background: #34495e;
    color: white;
}

.artwork-table th {
    padding: 15px;
    text-align: left;
    font-weight: 600;
}

.artwork-table td {
    padding: 12px 15px;
    border-bottom: 1px solid #ecf0f1;
}

.artwork-table tbody tr:hover {
    background: #f8f9fa;
}

.id-cell {
    font-family: 'Courier New', monospace;
    color: #7f8c8d;
    font-size: 0.9em;
}

.title-cell {
    font-weight: 500;
    color: #2c3e50;
}

.date-cell {
    white-space: nowrap;
}

.dimensions-cell {
    font-size: 0.9em;
    color: #7f8c8d;
}

.series-grid, .location-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
    gap: 20px;
    margin: 30px 0;
}

.series-card, .location-card {
    background: white;
    padding: 25px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    transition: transform 0.2s, box-shadow 0.2s;
}

.series-card:hover, .location-card:hover {
    transform: translateY(-5px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.15);
}

.series-card h3, .location-card h3 {
    color: #2c3e50;
    margin-bottom: 10px;
    font-size: 1.3em;
}

.series-count, .location-count {
    color: #3498db;
    font-weight: 600;
    font-size: 1.1em;
    margin-bottom: 5px;
}

.series-date {
    color: #7f8c8d;
    font-size: 0.9em;
}

footer {
    text-align: center;
    padding: 40px 20px;
    color: #7f8c8d;
    font-size: 0.9em;
}

  div.gallery 
  {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-start;
  }
  div.gallery-item
  {
    margin: 5px;
    border: 1px solid #ccc;
    width: 180px;
  }
  div.gallery-item:hover
  {
    border: 1px solid #777;
  }
  div.gallery-item img
  {
    width: 100%;
    height: auto;
  }
  div.gallery-item div.desc
  {
    padding: 5px;
    text-align: center;
  }



@media (max-width: 768px) {
    .artwork-table {
        font-size: 0.9em;
    }

    .artwork-table th, .artwork-table td {
        padding: 8px 10px;
    }

    h1 {
        font-size: 2em;
    }
}
";

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "style.css"), css);
    }

    private string GetHtmlHeader(string title)
    {
        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{EscapeHtml(title)}</title>
    <link rel='stylesheet' href='style.css'>
</head>
<body>";
    }

    private string GetHtmlFooter()
    {
        return $@"
    <footer>
        <p>Keith Long Archive | Generated {DateTime.Now:MMMM d, yyyy}</p>
    </footer>
</body>
</html>";
    }

    private string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&#39;");
    }
}

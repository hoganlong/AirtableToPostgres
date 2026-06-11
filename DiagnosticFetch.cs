using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace AirtableToPostgres;

public class DiagnosticFetch
{
    public static async Task Run(string recordId)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var apiKey = configuration["Airtable:ApiKey"];
        var baseId = configuration["Airtable:BaseId"];
        var tableName = "ARTWORK_IMAGE";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Fetch specific record
        var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}/{recordId}";

        Console.WriteLine($"Fetching: {url}");
        Console.WriteLine();

        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        Console.WriteLine("=== RAW AIRTABLE RESPONSE ===");
        Console.WriteLine(json.ToString(Formatting.Indented));
        Console.WriteLine();

        // Check for ARTWORK_ID field specifically
        var fields = json["fields"] as JObject;
        if (fields != null)
        {
            Console.WriteLine("=== ARTWORK_ID FIELD ANALYSIS ===");

            var artworkIdField = fields["ARTWORK_ID"];

            if (artworkIdField == null)
            {
                Console.WriteLine("❌ ARTWORK_ID field is MISSING from response");
                Console.WriteLine("   This means Airtable didn't send this field at all");
            }
            else if (artworkIdField.Type == JTokenType.Null)
            {
                Console.WriteLine("⚠️  ARTWORK_ID field is NULL");
            }
            else if (artworkIdField is JArray array)
            {
                Console.WriteLine($"✓ ARTWORK_ID is an array with {array.Count} elements");
                if (array.Count > 0)
                {
                    Console.WriteLine($"  First element: {array[0]}");
                }
                else
                {
                    Console.WriteLine("  Array is empty");
                }
            }
            else
            {
                Console.WriteLine($"? ARTWORK_ID has unexpected type: {artworkIdField.Type}");
                Console.WriteLine($"  Value: {artworkIdField}");
            }
        }
    }
}

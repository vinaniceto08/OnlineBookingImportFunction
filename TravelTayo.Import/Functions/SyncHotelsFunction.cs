using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelTayo.Import.Data;
using TravelTayo.Import.Models;

namespace TravelTayo.Import.Functions;

public class SyncHotelsFunction
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger _logger;
    private readonly string _apiKey;
    private readonly string _secret;

    public SyncHotelsFunction(AppDbContext db, IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = loggerFactory.CreateLogger<SyncHotelsFunction>();

        _apiKey = Environment.GetEnvironmentVariable("Hotelbeds_ApiKey") ?? throw new InvalidOperationException("Hotelbeds_ApiKey not set.");
        _secret = Environment.GetEnvironmentVariable("Hotelbeds_Secret") ?? throw new InvalidOperationException("Hotelbeds_Secret not set.");
    }

    // Timer trigger - runs every hour on the hour by default
    [Function("SyncHotelsTimer")]
    public async Task RunTimer([TimerTrigger("0 * * * * *")] string timerInfo)
    {
        _logger.LogInformation("[Timer] SyncHotelsTimer fired at {time}", DateTime.UtcNow);
        await ProcessAsync();
    }

    // HTTP trigger - manual trigger
    [Function("SyncHospManual")] // keep a short route name; function name differs from file name
    public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sync/hotels")] HttpRequestData req)
    {
        _logger.LogInformation("[HTTP] SyncHotelsManual invoked by {ip}", req.Headers?.GetValues("X-Forwarded-For").FirstOrDefault() ?? "local");
        await ProcessAsync();

        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteStringAsync("Hotel sync triggered.");
        return resp;
    }

    private (string timestamp, string xSignature) GenerateSignature()
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string data = _apiKey + _secret + timestamp;

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        string xSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return (timestamp, xSignature);
    }

    /// <summary>
    /// API Call
    /// </summary>
    /// <param name="hotel"></param>
    /// <returns></returns>
    //private async Task ProcessAsync()
    //{
    //    try
    //    {
    //        var client = _httpFactory.CreateClient("Hotelbeds");

    //        // Generate signature and set headers
    //        var (timestamp, signature) = GenerateSignature();

    //        client.DefaultRequestHeaders.Remove("Api-key");
    //        client.DefaultRequestHeaders.Remove("X-Signature");
    //        client.DefaultRequestHeaders.Remove("Accept");
    //        client.DefaultRequestHeaders.Add("Api-key", _apiKey);
    //        client.DefaultRequestHeaders.Add("X-Signature", signature);
    //        client.DefaultRequestHeaders.Add("Accept", "application/json");

    //        var fullUrl = "https://api.test.hotelbeds.com/hotel-content-api/1.0/hotels?fields=all&language=ENG&from=1&to=100&useSecondaryLanguage=false";

    //        using var resp = await client.GetAsync(fullUrl);
    //        if (!resp.IsSuccessStatusCode)
    //        {
    //            _logger.LogError("Hotelbeds API returned {status} {reason}", resp.StatusCode, resp.ReasonPhrase);
    //            return;
    //        }

    //        using var stream = await resp.Content.ReadAsStreamAsync();
    //        using var doc = await JsonDocument.ParseAsync(stream);

    //        if (!doc.RootElement.TryGetProperty("hotels", out var hotelsElement) || hotelsElement.ValueKind != JsonValueKind.Array)
    //        {
    //            _logger.LogWarning("Hotel list not found in response.");
    //            return;
    //        }

    //        int processed = 0;
    //        var batch = new List<Hotel>();

    //        foreach (var item in hotelsElement.EnumerateArray())
    //        {
    //            var h = await MapJsonToHotelAsync(item);
    //            if (h == null) continue;

    //            // Upsert by GiataCode
    //            Hotel? existing = null;
    //            if (h.GiataCode.HasValue)
    //            {
    //                existing = await _db.Hotels
    //                    .Include(x => x.Phones)
    //                    .Include(x => x.Wildcards)
    //                    .FirstOrDefaultAsync(x => x.GiataCode == h.GiataCode.Value);
    //            }

    //            if (existing != null)
    //            {
    //                // Update existing hotel
    //                existing.Name = h.Name;
    //                existing.Description = h.Description;
    //                existing.Email = h.Email;
    //                existing.Web = h.Web;
    //                existing.LastUpdate = DateTime.UtcNow;
    //                existing.License = h.License;
    //                existing.Ranking = h.Ranking;

    //                // Remove old child entities
    //                _db.HotelPhones.RemoveRange(existing.Phones);
    //                _db.HotelWildcards.RemoveRange(existing.Wildcards);

    //                // Re-add new child entities with navigation property
    //                foreach (var phone in h.Phones)
    //                    phone.Hotel = existing;

    //                foreach (var wildcard in h.Wildcards)
    //                    wildcard.Hotel = existing;

    //                _db.HotelPhones.AddRange(h.Phones);
    //                _db.HotelWildcards.AddRange(h.Wildcards);

    //                _db.Hotels.Update(existing);
    //                await _db.SaveChangesAsync();
    //            }
    //            else
    //            {
    //                // Ensure FKs for lookup tables (if any)
    //                await EnsureRequiredFKsAsync(h);

    //                // Set LastUpdate
    //                h.LastUpdate = DateTime.UtcNow;

    //                // Step 1: Add parent hotel first
    //                _db.Hotels.Add(h);
    //                await _db.SaveChangesAsync(); // generates the Hotel.Id for FKs

    //                // Step 2: Attach children with the generated HotelId
    //                foreach (var phone in h.Phones)
    //                    phone.HotelId = h.Id; // or use navigation property: phone.Hotel = h

    //                foreach (var wildcard in h.Wildcards)
    //                    wildcard.HotelId = h.Id;

    //                // Step 3: Save children
    //                _db.HotelPhones.AddRange(h.Phones);
    //                _db.HotelWildcards.AddRange(h.Wildcards);
    //                await _db.SaveChangesAsync();
    //            }

    //            processed++;
    //        }

    //        // Save remaining batch
    //        if (batch.Count > 0)
    //        {
    //            _db.Hotels.AddRange(batch);
    //            await _db.SaveChangesAsync();
    //            batch.Clear();
    //        }

    //        _logger.LogInformation("Processed {count} hotels.", processed);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error while processing hotels.");
    //    }
    //}
    private async Task ProcessAsync()
    {
        try
        {
            // Azure Blob settings

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("BlobContainerName");
            string blobName = Environment.GetEnvironmentVariable("BlobFileName");

            // Create blob client
            var blobClient = new BlobClient(connectionString, containerName, blobName);

            // Check if blob exists
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogError("Blob '{blobName}' not found in container '{containerName}'", blobName, containerName);
                return;
            }

            // Download blob content
            var downloadInfo = await blobClient.DownloadAsync();

            using var stream = downloadInfo.Value.Content;
            using var reader = new StreamReader(stream);
            string content = await reader.ReadToEndAsync();

            // Parse content (assuming it's JSON, like the API)
            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("hotels", out var hotelsElement) || hotelsElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Hotel list not found in blob.");
                return;
            }

            int processed = 0;
            var batch = new List<Hotel>();

            foreach (var item in hotelsElement.EnumerateArray())
            {
                var h = await MapJsonToHotelAsync(item);
                if (h == null) continue;

                // Upsert logic (same as your current code)
                Hotel? existing = null;
                if (h.GiataCode.HasValue)
                {
                    existing = await _db.Hotels
                        .Include(x => x.Phones)
                        .Include(x => x.Wildcards)
                        .FirstOrDefaultAsync(x => x.GiataCode == h.GiataCode.Value);
                }

                if (existing != null)
                {
                    // Update existing hotel
                    existing.Name = h.Name;
                    existing.Description = h.Description;
                    existing.Email = h.Email;
                    existing.Web = h.Web;
                    existing.LastUpdate = DateTime.UtcNow;
                    existing.License = h.License;
                    existing.Ranking = h.Ranking;


                    // --- Phones ---
                    // Remove only existing phones that are in DB
                    var phonesToDelete = existing.Phones.Where(p => _db.Entry(p).IsKeySet).ToList();
                    _db.HotelPhones.RemoveRange(phonesToDelete);

                    // Add new phones
                    existing.Phones = h.Phones;

                    // --- Wildcards ---
                    var wildcardsToDelete = existing.Wildcards.Where(w => _db.Entry(w).IsKeySet).ToList();
                    _db.HotelWildcards.RemoveRange(wildcardsToDelete);
                    existing.Wildcards = h.Wildcards;

                    _db.Hotels.Update(existing);
                    await _db.SaveChangesAsync();
                }
                else
                {

                    _db.Hotels.Add(h);
                    await _db.SaveChangesAsync();   // Save parent first to obtain h.Id


                    //... inside `else` block for new hotel...
                   await EnsureRequiredFKsAsync(h);
                   h.LastUpdate = DateTime.UtcNow;


                    // Link each new Phone/Wildcard to the saved Hotel
                    foreach (var phone in h.Phones) phone.Hotel = h;       // or phone.HotelId = h.Id;
                    foreach (var wildcard in h.Wildcards) wildcard.Hotel = h; // or wildcard.HotelId = h.Id

                    // Add child entities and save them
                    //_db.HotelPhones.AddRange(h.Phones);
                    //_db.HotelWildcards.AddRange(h.Wildcards);
                    //await _db.SaveChangesAsync();

                }

                processed++;
            }

            _logger.LogInformation("Processed {count} hotels from blob.", processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing hotels from blob.");
        }
    }


    private async Task EnsureRequiredFKsAsync(Hotel hotel)
    {
        // === COUNTRY ===
        if (hotel.Country != null)
        {
            var country = await _db.Countries
                .FirstOrDefaultAsync(c => c.Code == hotel.Country.Code);

            if (country == null)
            {
                country = new Country
                {
                    Code = hotel.Country.Code,
                    Description = hotel.Country.Description ?? hotel.Country.Code,
                    IsoCode = hotel.Country.IsoCode ?? hotel.Country.Code // fallback
                };
                _db.Countries.Add(country);
                await _db.SaveChangesAsync();
            }

            hotel.CountryId = country.Id;
        }

        // === CATEGORY ===
        if (hotel.Category != null)
        {
            var category = await _db.Categories
                .FirstOrDefaultAsync(c => c.Code == hotel.Category.Code);

            if (category == null)
            {
                category = new Category
                {
                    Code = hotel.Category.Code,
                    Description = hotel.Category.Description ?? hotel.Category.Code
                };
                _db.Categories.Add(category);
                await _db.SaveChangesAsync();
            }

            hotel.CategoryId = category.Id;
        }


        // === ACCOMMODATION TYPE ===
        if (hotel.AccommodationType != null)
        {
            var type = await _db.AccommodationTypes
                .FirstOrDefaultAsync(a => a.Code == hotel.AccommodationType.Code);

            if (type == null)
            {
                type = new AccommodationType
                {
                    Code = hotel.AccommodationType.Code
                    // Add default values for any other non-nullable columns if needed
                };
                _db.AccommodationTypes.Add(type);
                await _db.SaveChangesAsync();
            }

            hotel.AccommodationTypeId = type.Id;
        }

        // === HOTELS PHONES ===
        if (hotel.Phones != null)
        {
            foreach (var phone in hotel.Phones)
            {
                if (string.IsNullOrWhiteSpace(phone.Phone))
                    continue;

                if (!_db.HotelPhones.Any(p => p.HotelId == hotel.Id && p.Phone == phone.Phone))
                {
                    _db.HotelPhones.Add(new HotelPhone
                    {
                        HotelId = hotel.Id,
                        Phone = phone.Phone
                    });
                }
            }
        }

        // === HOTELS WILDCARDS ===
        if (hotel.Wildcards != null)
        {
            foreach (var wc in hotel.Wildcards)
            {
                if (!_db.HotelWildcards.Any(w => w.HotelId == hotel.Id && w.Value == wc.Value))
                {
                    _db.HotelWildcards.Add(new HotelWildcard
                    {
                        HotelId = hotel.Id,
                        Value = wc.Value
                    });
                }
            }
        }

        // Finally, save all changes
        await _db.SaveChangesAsync();
    }


    private string? GetJsonString(JsonElement element, string? fallbackProperty = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                return element.GetRawText(); // converts number to string

            case JsonValueKind.Object:
                if (fallbackProperty != null && element.TryGetProperty(fallbackProperty, out var nested))
                    return GetJsonString(nested);
                break;
        }

        return null;
    }

    private async Task<Hotel?> MapJsonToHotelAsync(JsonElement item)
    {
        try
        {
            int giataCode = 0;
            Hotel? existing = null;

            if (item.TryGetProperty("giataCode", out var giataProp) && giataProp.ValueKind == JsonValueKind.Number)
            {
                giataCode = giataProp.GetInt32();
                existing = await _db.Hotels
                    .Include(x => x.Phones)
                    .Include(x => x.Wildcards)
                    .AsSplitQuery() // avoids multiple collection join issues
                    .FirstOrDefaultAsync(x => x.GiataCode == giataCode);
            }
            var hotel = existing ?? new Hotel();

            // --- GiataCode ---
            hotel.GiataCode = giataCode;  

            // --- Name ---
            if (item.TryGetProperty("name", out var nameProp))
            {
                if (nameProp.ValueKind == JsonValueKind.String)
                    hotel.Name = nameProp.GetString();
                else if (nameProp.ValueKind == JsonValueKind.Object && nameProp.TryGetProperty("content", out var n))
                    hotel.Name = n.GetString();
            }

            // --- Description ---
            if (item.TryGetProperty("description", out var descProp))
            {
                if (descProp.ValueKind == JsonValueKind.String)
                    hotel.Description = descProp.GetString();
                else if (descProp.ValueKind == JsonValueKind.Object && descProp.TryGetProperty("content", out var d))
                    hotel.Description = d.GetString();
            }

            // --- Website & Email ---
            if (item.TryGetProperty("web", out var webProp) && webProp.ValueKind == JsonValueKind.String)
                hotel.Web = webProp.GetString();

            if (item.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                var EmailValue = emailProp.GetString();
                hotel.Email = string.IsNullOrWhiteSpace(EmailValue) ? "NA" : EmailValue;
            }
            else
            {
                hotel.Email = "NA";
            }

            // --- License & Ranking ---
            if (item.TryGetProperty("license", out var licProp) && licProp.ValueKind == JsonValueKind.String)
            {
                var licenseValue = licProp.GetString();
                hotel.License = string.IsNullOrWhiteSpace(licenseValue) ? "NA" : licenseValue;
            }
            else
            {
                hotel.License = "NA";
            }
            if (item.TryGetProperty("ranking", out var rankProp) && rankProp.TryGetInt32(out var rk))
                hotel.Ranking = rk;

            if (item.TryGetProperty("S2C", out var S2CProp) && S2CProp.ValueKind == JsonValueKind.String)
                hotel.S2C = S2CProp.GetString();

            // --- Address ---
            if (item.TryGetProperty("address", out var addrProp))
            {
                var address = new Address();
                if (addrProp.TryGetProperty("content", out var contentProp))
                    address.Content = contentProp.GetString();
                if (addrProp.TryGetProperty("street", out var streetProp))
                    address.Street = streetProp.GetString();
                if (addrProp.TryGetProperty("number", out var numberProp))
                    address.Number = numberProp.GetString();

                // Extract postalCode from top-level JSON
                if (item.TryGetProperty("postalCode", out var postalProp))
                    address.PostalCode = postalProp.GetString();

                // Extract city from top-level JSON
                if (item.TryGetProperty("city", out var cityProp))
                {
                    if (cityProp.TryGetProperty("content", out var cityContent))
                        address.City = cityContent.GetString();
                }

                hotel.Address = address;
            }


            // --- Phones ---
            if (item.TryGetProperty("phones", out var phonesProp) && phonesProp.ValueKind == JsonValueKind.Array)
            {
                var phones = new List<HotelPhone>();
                foreach (var p in phonesProp.EnumerateArray())
                {
                    if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("phoneNumber", out var numProp) && numProp.ValueKind == JsonValueKind.String)
                        phones.Add(new HotelPhone { Phone = numProp.GetString() });
                }
                hotel.Phones = phones;
            }

            // --- Wildcards / Tags ---
            if (item.TryGetProperty("wildcards", out var wildProp) && wildProp.ValueKind == JsonValueKind.Array)
            {
                var wilds = new List<HotelWildcard>();
                foreach (var w in wildProp.EnumerateArray())
                {
                    if (w.ValueKind == JsonValueKind.String)
                        wilds.Add(new HotelWildcard { Value = w.GetString() });
                }
                hotel.Wildcards = wilds;
            }

            // --- Country ---
            // 4️⃣ Map country safely
            if (item.TryGetProperty("countryCode", out var ccProp))
            {
                string code = ccProp.GetString() ?? "UNK";

                // Try to get existing country first
                var country = await _db.Countries.FirstOrDefaultAsync(c => c.Code == code);
                if (country == null)
                {
                    country = new Country
                    {
                        Code = code,
                        Description = code,  // or a proper mapping if you have one
                        IsoCode = code       // must not be null
                    };
                    _db.Countries.Add(country);
                }

                hotel.Country = country;
            }

            // --- Optional codes for FK mapping ---
            if (item.TryGetProperty("stateCode", out var stateProp) && stateProp.ValueKind == JsonValueKind.String)
                hotel.State = new State { Code = stateProp.GetString(), Name = stateProp.GetString() };

            if (item.TryGetProperty("zoneCode", out var zoneProp) && zoneProp.ValueKind == JsonValueKind.Number && zoneProp.TryGetInt32(out var z))
                hotel.Zone = new Zone { Name = z.ToString(), Description = "" };

            if (item.TryGetProperty("categoryCode", out var catProp) && catProp.ValueKind == JsonValueKind.String)
                hotel.Category = new Category { Code = catProp.GetString(), Description = catProp.GetString() };

            if (item.TryGetProperty("categoryGroupCode", out var cgProp) && cgProp.ValueKind == JsonValueKind.String)
                hotel.CategoryGroup = new CategoryGroup { Code = cgProp.GetString(), Description = cgProp.GetString() };

            if (item.TryGetProperty("accommodationTypeCode", out var atProp) && atProp.ValueKind == JsonValueKind.String)
                hotel.AccommodationType = new AccommodationType { Code = atProp.GetString() };


            hotel.LastUpdate = DateTime.Now; // set/update timestamp

            return hotel;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map JSON to Hotel.");
            return null;
        }
    }


}

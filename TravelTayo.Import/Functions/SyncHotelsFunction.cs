using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public async Task RunTimer([TimerTrigger("0 0 * * * *")] string timerInfo)
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

    private async Task ProcessAsync()
    {
        try
        {
            var client = _httpFactory.CreateClient("Hotelbeds");
            // generate signature and set headers for this request
            var (timestamp, signature) = GenerateSignature();

            client.DefaultRequestHeaders.Remove("Api-key");
            client.DefaultRequestHeaders.Remove("X-Signature");
            client.DefaultRequestHeaders.Remove("Accept");
            client.DefaultRequestHeaders.Add("Api-key", _apiKey);
            client.DefaultRequestHeaders.Add("X-Signature", signature);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            // full endpoint (the hostname is not set in client, so use absolute)
            var fullUrl = "https://api.test.hotelbeds.com/hotel-content-api/1.0/hotels?fields=all&language=ENG&from=1&to=100&useSecondaryLanguage=false";

            using var resp = await client.GetAsync(fullUrl);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Hotelbeds API returned {status} {reason}", resp.StatusCode, resp.ReasonPhrase);
                return;
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("hotels", out var hotelsElement) || hotelsElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("hotel list not found in response.");
                return;
            }

            int processed = 0;
            var batch = new List<Hotel>();
            foreach (var item in hotelsElement.EnumerateArray())
            {
                var h = await MapJsonToHotelAsync(item);
                if (h == null) continue;

                // Upsert by GiataCode
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
                    existing.Name = h.Name;
                    existing.Description = h.Description;
                    existing.Email = h.Email;
                    existing.Web = h.Web;
                    existing.LastUpdate = DateTime.UtcNow;
                    existing.License = h.License;
                    existing.Ranking = h.Ranking;
                    // Replace phones and wildcards (simple approach)
                    _db.HotelPhones.RemoveRange(existing.Phones);
                    _db.HotelWildcards.RemoveRange(existing.Wildcards);

                    existing.Phones = h.Phones;
                    existing.Wildcards = h.Wildcards;

                    _db.Hotels.Update(existing);
                }
                else
                {
                    // Ensure required FKs exist: Country, State, Destination, etc.
                    await EnsureRequiredFKsAsync(h);
                    h.LastUpdate = DateTime.UtcNow;
                    batch.Add(h);
                }

                processed++;

                // Save in batches to avoid huge transactions
                if (batch.Count >= 50)
                {
                    _db.Hotels.AddRange(batch);
                    await _db.SaveChangesAsync();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                _db.Hotels.AddRange(batch);
                await _db.SaveChangesAsync();
                batch.Clear();
            }

            _logger.LogInformation("Processed {count} hotels.", processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing hotels.");
        }
    }

    private async Task EnsureRequiredFKsAsync(Hotel hotel)
    {
        // If CountryId is 0, create/find a default "UNKNOWN" and set
        if (hotel.CountryId == 0)
        {
            var code = hotel.Country?.Code ?? "UNK";
            var country = await _db.Countries.FirstOrDefaultAsync(c => c.Code == code);
            if (country == null)
            {
                country = new Country { Code = code, Description = "Unknown" };
                _db.Countries.Add(country);
                await _db.SaveChangesAsync();
            }
            hotel.CountryId = country.Id;
            hotel.Country = country;
        }

        if (hotel.AddressId == 0)
        {
            // create empty address if none
            var address = new Address { Content = hotel.Address?.Content ?? "Unknown", City = hotel.Address?.City, Street = hotel.Address?.Street, Number = hotel.Address?.Number, PostalCode = hotel.Address?.PostalCode };
            _db.Addresses.Add(address);
            await _db.SaveChangesAsync();
            hotel.AddressId = address.Id;
            hotel.Address = address;
        }

        // Similar minimal checks for State/Destination/Zone/etc can be added:
        if (hotel.StateId == 0)
        {
            var st = new State { Code = hotel.State?.Code ?? "UNK", Name = hotel.State?.Name ?? "Unknown" };
            _db.States.Add(st);
            await _db.SaveChangesAsync();
            hotel.StateId = st.Id;
            hotel.State = st;
        }

        // for Destination, Zone, Category, CategoryGroup, Chain, AccommodationType: set to defaults if 0
        if (hotel.DestinationId == 0)
        {
            var d = new Destination { Code = hotel.Destination?.Code ?? "UNK", Name = hotel.Destination?.Name ?? "Unknown" };
            _db.Destinations.Add(d);
            await _db.SaveChangesAsync();
            hotel.DestinationId = d.Id;
            hotel.Destination = d;
        }

        if (hotel.ZoneId == 0)
        {
            var z = new Zone { Name = hotel.Zone?.Name ?? "Unknown", Description = hotel.Zone?.Description ?? "" };
            _db.Zones.Add(z);
            await _db.SaveChangesAsync();
            hotel.ZoneId = z.Id;
            hotel.Zone = z;
        }

        if (hotel.CategoryId == 0)
        {
            var c = new Category { Code = hotel.Category?.Code ?? "UNK", Description = hotel.Category?.Description ?? "Unknown" };
            _db.Categories.Add(c);
            await _db.SaveChangesAsync();
            hotel.CategoryId = c.Id;
            hotel.Category = c;
        }

        if (hotel.CategoryGroupId == 0)
        {
            var cg = new CategoryGroup { Code = hotel.CategoryGroup?.Code ?? "UNK", Description = hotel.CategoryGroup?.Description ?? "Unknown" };
            _db.CategoryGroups.Add(cg);
            await _db.SaveChangesAsync();
            hotel.CategoryGroupId = cg.Id;
            hotel.CategoryGroup = cg;
        }

        if (hotel.ChainId == 0)
        {
            var ch = new Chain { Code = hotel.Chain?.Code ?? "UNK", Description = hotel.Chain?.Description ?? "Unknown" };
            _db.Chains.Add(ch);
            await _db.SaveChangesAsync();
            hotel.ChainId = ch.Id;
            hotel.Chain = ch;
        }

        if (hotel.AccommodationTypeId == 0)
        {
            var at = new AccommodationType { Code = hotel.AccommodationType?.Code ?? "UNK", TypeDescription = hotel.AccommodationType?.TypeDescription ?? "Unknown" };
            _db.AccommodationTypes.Add(at);
            await _db.SaveChangesAsync();
            hotel.AccommodationTypeId = at.Id;
            hotel.AccommodationType = at;
        }
    }

    private async Task<Hotel?> MapJsonToHotelAsync(JsonElement item)
    {
        try
        {
            var hotel = new Hotel();

            if (item.TryGetProperty("name", out var nameProp))
                hotel.Name = nameProp.GetString() ?? hotel.Name;

            // description may be object or string
            if (item.TryGetProperty("description", out var descProp))
            {
                if (descProp.ValueKind == JsonValueKind.String)
                    hotel.Description = descProp.GetString();
                else if (descProp.ValueKind == JsonValueKind.Object && descProp.TryGetProperty("content", out var contentProp))
                    hotel.Description = contentProp.GetString();
            }

            if (item.TryGetProperty("web", out var webProp))
                hotel.Web = webProp.GetString();

            if (item.TryGetProperty("email", out var emailProp))
                hotel.Email = emailProp.GetString();

            if (item.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out var c))
                hotel.GiataCode = c;
            else if (item.TryGetProperty("giataCode", out var giataProp) && giataProp.TryGetInt32(out var g))
                hotel.GiataCode = g;

            // address object
            if (item.TryGetProperty("address", out var addrProp) && addrProp.ValueKind == JsonValueKind.Object)
            {
                var addr = new Address();
                if (addrProp.TryGetProperty("street", out var st)) addr.Street = st.GetString();
                if (addrProp.TryGetProperty("number", out var num)) addr.Number = num.GetString();
                if (addrProp.TryGetProperty("postalCode", out var pc)) addr.PostalCode = pc.GetString();
                if (addrProp.TryGetProperty("city", out var city)) addr.City = city.GetString();
                if (addrProp.TryGetProperty("content", out var content)) addr.Content = content.GetString();
                hotel.Address = addr;
            }

            // phones array
            if (item.TryGetProperty("phones", out var phonesProp) && phonesProp.ValueKind == JsonValueKind.Array)
            {
                var phones = new List<HotelPhone>();
                foreach (var p in phonesProp.EnumerateArray())
                {
                    string? value = null;
                    if (p.ValueKind == JsonValueKind.String) value = p.GetString();
                    else if (p.ValueKind == JsonValueKind.Object)
                    {
                        if (p.TryGetProperty("phoneNumber", out var pn)) value = pn.GetString();
                        else if (p.TryGetProperty("phone", out var pn2)) value = pn2.GetString();
                        else if (p.TryGetProperty("number", out var pn3)) value = pn3.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                        phones.Add(new HotelPhone { Phone = value });
                }
                hotel.Phones = phones;
            }

            // wildcards: example fallback if "wildcards" field exists or tags
            if (item.TryGetProperty("wildcards", out var wildProp) && wildProp.ValueKind == JsonValueKind.Array)
            {
                var w = new List<HotelWildcard>();
                foreach (var wd in wildProp.EnumerateArray())
                {
                    var s = wd.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) w.Add(new HotelWildcard { Value = s });
                }
                hotel.Wildcards = w;
            }

            // license / ranking if present
            if (item.TryGetProperty("license", out var licProp))
                hotel.License = licProp.GetString();

            if (item.TryGetProperty("ranking", out var rankProp) && rankProp.TryGetInt32(out var rk))
                hotel.Ranking = rk;

            // Country code
            if (item.TryGetProperty("countryCode", out var ccProp))
            {
                var country = new Country { Code = ccProp.GetString() ?? "UNK", Description = ccProp.GetString() };
                hotel.Country = country; // will ensure and save in EnsureRequiredFKsAsync
            }

            // other optional mapping (destination, zone, category etc) omitted for brevity
            return hotel;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map JSON to Hotel.");
            return null;
        }
    }
}

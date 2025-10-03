//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using System;
//using TravelTayo.Import.Data;

//var host = new HostBuilder()
//    .ConfigureFunctionsWebApplication()
//    .ConfigureServices((context, services) =>
//    {
//        // Read environment variables
//        var sql = Environment.GetEnvironmentVariable("SqlConnectionString")
//                  ?? throw new InvalidOperationException("SqlConnectionString is not set.");

//        var hotelbedsApiKey = Environment.GetEnvironmentVariable("Hotelbeds_ApiKey");
//        if (string.IsNullOrEmpty(hotelbedsApiKey))
//        {
//            // not fatal here if running in CI; you can still test but HttpClient will fail at runtime
//            Console.WriteLine("Warning: Hotelbeds_ApiKey not set in environment.");
//        }

//        // EF Core
//        services.AddDbContext<AppDBContext>(options =>
//            options.UseSqlServer(sql));

//        // HttpClient with API key header (named client)
//        services.AddHttpClient("Hotelbeds", client =>
//        {
//            client.BaseAddress = new Uri("https://api.test.hotelbeds.com/");
//            if (!string.IsNullOrEmpty(hotelbedsApiKey))
//            {
//                client.DefaultRequestHeaders.Add("Api-key", hotelbedsApiKey);
//            }
//            // If the API requires other headers, add them here (e.g. signature)
//        });

//        // HttpClientFactory for general use
//        services.AddHttpClient();
//    })
//    .Build();

//host.Run();
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using TravelTayo.Import.Data;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var sql = Environment.GetEnvironmentVariable("SqlConnectionString")
                  ?? throw new InvalidOperationException("SqlConnectionString not found in environment.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(sql, sqlOptions =>
            {
                // configure if needed
            }));

        // Named HttpClient (base not required since full URL used)
        services.AddHttpClient("Hotelbeds");

        // Add logging, etc. (already included by worker)
    })
    .Build();

host.Run();


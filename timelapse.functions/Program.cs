using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using timelapse.core.models;
using timelapse.functions.infrastructure;
using timelapse.functions.services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // services.AddApplicationInsightsTelemetryWorkerService();
        // services.ConfigureFunctionsApplicationInsights();

        // Add Entity Framework
        var connectionString = context.Configuration["ConnectionStrings:DefaultConnection"];
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Register services with their interfaces
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<VideoProcessingService>();

        // Add HttpClient
        services.AddHttpClient();
    })
    .Build();

host.Run();

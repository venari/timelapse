using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using timelapse.core.models;
using timelapse.functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Add Entity Framework
        var connectionString = context.Configuration["ConnectionStrings:DefaultConnection"];
        services.AddDbContext<TimelapseDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Add services
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<VideoProcessingService>();

        // Add HttpClient
        services.AddHttpClient();
    })
    .Build();

host.Run();

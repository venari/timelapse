// See https://aka.ms/new-console-template for more information
using extract.imagery.infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;


// Define logger
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole();
});

// Define configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();


ILogger logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation(configuration.GetConnectionString("DefaultConnection"));

using (var dbContext = new AppDbContext(configuration))
{
    var eventsCount = dbContext.Events.Count();
    Console.WriteLine(eventsCount);
}
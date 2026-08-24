using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using timelapse.api.Areas.Identity.Data;
using timelapse.Services;
using Microsoft.OpenApi.Models;
using timelapse.api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDefaultIdentity<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// SameSite=None (with Secure, since the app already forces HTTPS via UseHttpsRedirection)
// so the auth cookie still flows to the API from the Vite dev server, which - because it's
// served over plain http while the API is https - counts as cross-site under browsers'
// schemeful-same-site rules and wouldn't otherwise receive a Lax/Strict cookie at all. Safe
// for the existing same-site Razor Pages flows too: None is a strict superset of Lax.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "api-key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

});

builder.Services.AddControllers()
    .AddJsonOptions(options =>{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// builder.Services.AddEnvironmentVariables();
// builder.AddEnvironmentVariables();

builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddScoped<timelapse.api.Services.DeviceUpdateService>();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.Configure<AuthMessageSenderOptions>(builder.Configuration);

// Configure CORS for React front-end
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Vite default port is 5173
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowReactApp");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// The React app (timelapse.web) is now the default UI. The old Razor Pages Index
// page moved to /legacy (see Pages/Index.cshtml's @page directive) so it stays
// fully reachable; every other old URL is untouched.
app.MapGet("/", () => Results.Redirect("/dashboard"));

app.MapFallbackToFile("/dashboard", "dist/index.html");
app.MapFallbackToFile("/dashboard/{*path}", "dist/index.html");
app.MapFallbackToFile("/device/{*path}", "dist/index.html");
app.MapFallbackToFile("/image-view/{*path}", "dist/index.html");
app.MapFallbackToFile("/telemetry/{*path}", "dist/index.html");
app.MapFallbackToFile("/login", "dist/index.html");
// "event" (singular) deliberately, not "events" - the old Razor "Events" folder's
// Index page has an implicit bare-folder alias plus its own optional int route
// parameter, so (combined with ASP.NET's case-insensitive routing) "/events" and
// "/events/{number}" already resolve to that old page; "event" never collides with it.
app.MapFallbackToFile("/event", "dist/index.html");
app.MapFallbackToFile("/event/{*path}", "dist/index.html");

app.MapSwagger();
app.UseSwaggerUI();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    // endpoints.MapRazorPages();
});

try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();    
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        DbInitializer.Initialize(context, userManager, roleManager);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while seeding the database.");
}


app.Run();

using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Server.Services;
using SereniTeam.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configure Entity Framework with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Handle Render.com or Heroku DATABASE_URL format
if (connectionString.StartsWith("postgres://"))
{
    connectionString = ConvertPostgresUrl(connectionString);
}

builder.Services.AddDbContext<SereniTeamContext>(options =>
    options.UseNpgsql(connectionString));

// Register application services
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<ITeamService, TeamService>();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// app.UseHttpsRedirection(); 

// Enable CORS
app.UseCors("AllowBlazorWasm");

// Routing comes AFTER static files
app.UseRouting();

// Map endpoints
app.MapRazorPages();
app.MapControllers();
app.MapHub<TeamUpdatesHub>("/teamupdates");

// Fallback MUST be last
app.MapFallbackToFile("index.html");

// Database setup...
using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<SereniTeamContext>();
await context.Database.MigrateAsync();
await SeedInitialData(context);

await app.RunAsync();

/// <summary>
/// Converts postgres:// URL format to connection string format
/// </summary>
static string ConvertPostgresUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');

    return $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.Substring(1)};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

/// <summary>
/// Seeds initial development data
/// </summary>
static async Task SeedInitialData(SereniTeamContext context)
{
    if (await context.Teams.AnyAsync())
        return; // Data already exists

    var teams = new[]
    {
        new SereniTeam.Shared.Models.Team
        {
            Name = "Development Team",
            Description = "Software development team working on core products",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        },
        new SereniTeam.Shared.Models.Team
        {
            Name = "Marketing Team",
            Description = "Marketing and communications team",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        }
    };

    context.Teams.AddRange(teams);
    await context.SaveChangesAsync();
}
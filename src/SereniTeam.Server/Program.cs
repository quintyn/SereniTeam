using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Server.Services;
using SereniTeam.Server.Hubs;
using SereniTeam.Client.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("=== SereniTeam Starting (Blazor Server Mode) ===");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Content Root: {builder.Environment.ContentRootPath}");
Console.WriteLine($"Web Root: {builder.Environment.WebRootPath}");

// Add services for Blazor Server (not WebAssembly)
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    // Configure SignalR for Blazor Server
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitMaxRetained = 100;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
});

// Add API controllers for Swagger/API access
builder.Services.AddControllers();

// Configure Entity Framework with PostgreSQL - FIXED for Azure
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]  // Azure format
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")  // Azure env var format
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("WARNING: No database connection string found. App will start but database features will be limited.");
    // Use a default connection string that won't work but allows startup
    connectionString = "Host=localhost;Database=dummy;Username=dummy;Password=dummy";
}

// Log connection attempt (hide password for security)
var logConnectionString = connectionString.Contains("Password=")
    ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***"
    : connectionString;
Console.WriteLine($"Using connection string: {logConnectionString}");

// Handle Render.com or Heroku DATABASE_URL format
if (connectionString.StartsWith("postgres://"))
{
    connectionString = ConvertPostgresUrl(connectionString);
}

builder.Services.AddDbContext<SereniTeamContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Add retry logic for Azure
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
});

// Register application services (business logic)
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<ITeamService, TeamService>();

// FIXED: Register the Blazor Server API services (these replace HTTP calls)
// These implement the same interfaces as the WebAssembly versions but call services directly
builder.Services.AddScoped<SereniTeam.Client.Services.ITeamApiService>(provider =>
{
    var teamService = provider.GetRequiredService<ITeamService>();
    var logger = provider.GetRequiredService<ILogger<ServerSideTeamApiService>>();
    return new ServerSideTeamApiService(teamService, logger);
});

builder.Services.AddScoped<SereniTeam.Client.Services.ICheckInApiService>(provider =>
{
    var checkInService = provider.GetRequiredService<ICheckInService>();
    var logger = provider.GetRequiredService<ILogger<ServerSideCheckInApiService>>();
    return new ServerSideCheckInApiService(checkInService, logger);
});

// Add SignalR for real-time updates
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.StreamBufferCapacity = 10;
});

// Add Health checks
builder.Services.AddHealthChecks();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SereniTeam API", Version = "v1" });
});

var app = builder.Build();

Console.WriteLine("=== Configuring Middleware Pipeline (Blazor Server) ===");

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("Development mode");
    app.UseDeveloperExceptionPage();
}
else
{
    Console.WriteLine("Production mode - using exception handler");
    app.UseExceptionHandler("/Error");
}

// Add detailed error endpoint to see what's failing
app.Map("/Error", async (HttpContext context) =>
{
    var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    if (exceptionFeature?.Error != null)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exceptionFeature.Error, "Unhandled exception on path: {Path}", exceptionFeature.Path);

        return Results.Problem(
            title: "An error occurred",
            detail: app.Environment.IsDevelopment() ? exceptionFeature.Error.ToString() : "Internal server error",
            statusCode: 500
        );
    }
    return Results.Problem("Unknown error occurred");
});

// Always enable Swagger for demo purposes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SereniTeam API v1");
    c.RoutePrefix = "swagger"; // Access at /swagger
});

// Static files (much simpler for Blazor Server - no _framework directory needed)
Console.WriteLine("Setting up static file serving (Blazor Server mode)...");
app.UseStaticFiles();

// Routing middleware
app.UseRouting();

Console.WriteLine("Mapping endpoints...");

// Health check endpoints
app.MapHealthChecks("/health");
app.MapGet("/health/simple", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    mode = "Blazor Server"
}));

// Debug endpoint to check file system
app.MapGet("/debug/files", () =>
{
    try
    {
        var webRootPath = app.Environment.WebRootPath ?? "wwwroot";
        var contentRootPath = app.Environment.ContentRootPath;

        var files = new
        {
            Mode = "Blazor Server",
            ContentRootPath = contentRootPath,
            WebRootPath = webRootPath,
            WebRootExists = Directory.Exists(webRootPath),
            WebRootFiles = Directory.Exists(webRootPath)
                ? Directory.GetFiles(webRootPath).Select(Path.GetFileName).ToArray()
                : new string[0],
            Note = "Blazor Server doesn't need _framework directory"
        };

        return Results.Ok(files);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error checking files: {ex.Message}");
    }
});

// Database health check (with error handling)
app.MapGet("/health/db", async (SereniTeamContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            var teamCount = await context.Teams.CountAsync();
            var checkInCount = await context.CheckIns.CountAsync();
            return Results.Ok(new
            {
                status = "healthy",
                database = "connected",
                teams = teamCount,
                checkIns = checkInCount
            });
        }
        return Results.Problem("Database connection failed");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database error: {ex.Message}");
    }
});

// API endpoints (keep these for API access)
app.MapControllers();

// SignalR hub
app.MapHub<TeamUpdatesHub>("/teamupdates");

// Blazor Server endpoints (these replace the WebAssembly fallback)
app.MapBlazorHub("/blazorhub"); // SignalR hub for Blazor Server
app.MapRazorPages(); // For _Host.cshtml
app.MapFallbackToPage("/_Host"); // Host page for Blazor Server

Console.WriteLine("=== Endpoint mapping complete ===");

// Database setup with improved error handling
await SetupDatabase(app);

Console.WriteLine("=== SereniTeam Ready (Blazor Server) ===");
Console.WriteLine("Access points:");
Console.WriteLine("- Main App: /");
Console.WriteLine("- API Docs: /swagger");
Console.WriteLine("- Health: /health");
Console.WriteLine("- Database Health: /health/db");
Console.WriteLine("- File Debug: /debug/files");

await app.RunAsync();

/// <summary>
/// Database setup with comprehensive error handling
/// </summary>
static async Task SetupDatabase(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SereniTeamContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting database setup...");

        // Test connection first
        logger.LogInformation("Testing database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogWarning("Cannot connect to database - continuing without database features");
            return;
        }
        logger.LogInformation("Database connection successful");

        // Apply migrations
        logger.LogInformation("Checking for pending migrations...");
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations: {Migrations}",
                pendingMigrations.Count(), string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully");
        }
        else
        {
            logger.LogInformation("No pending migrations found");
        }

        // Seed initial data
        logger.LogInformation("Checking for seed data...");
        await SeedDemoData(context, logger);

        logger.LogInformation("Database setup completed successfully");
    }
    catch (Exception ex)
    {
        var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
        logger.LogError(ex, "Database setup failed: {Message}", ex.Message);

        // In production, log the error but continue startup
        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine($"Database setup failed in development: {ex}");
            // Continue anyway for demo purposes
        }
        else
        {
            logger.LogWarning("Continuing startup despite database setup failure...");
        }
    }
}

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
/// Seeds demo data for the application
/// </summary>
static async Task SeedDemoData(SereniTeamContext context, ILogger logger)
{
    try
    {
        if (await context.Teams.AnyAsync())
        {
            logger.LogInformation("Teams already exist, skipping seed data");
            return;
        }

        logger.LogInformation("Creating demo teams...");

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
            },
            new SereniTeam.Shared.Models.Team
            {
                Name = "Design Team",
                Description = "UI/UX design and creative team",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

        context.Teams.AddRange(teams);
        await context.SaveChangesAsync();

        logger.LogInformation("Successfully created {Count} demo teams", teams.Length);

        // Add some demo check-ins for demonstration
        var checkIns = new List<SereniTeam.Shared.Models.CheckIn>();
        var random = new Random();

        foreach (var team in teams)
        {
            // Add some random check-ins for the past week
            for (int day = 0; day < 7; day++)
            {
                for (int checkin = 0; checkin < random.Next(1, 4); checkin++)
                {
                    checkIns.Add(new SereniTeam.Shared.Models.CheckIn
                    {
                        TeamId = team.Id,
                        MoodRating = random.Next(4, 9), // Generally positive mood
                        StressLevel = random.Next(2, 7), // Moderate stress
                        Notes = day == 0 ? "Demo check-in data" : null,
                        SubmittedAt = DateTime.UtcNow.AddDays(-day).AddHours(-random.Next(0, 8))
                    });
                }
            }
        }

        if (checkIns.Any())
        {
            context.CheckIns.AddRange(checkIns);
            await context.SaveChangesAsync();
            logger.LogInformation("Created {Count} demo check-ins", checkIns.Count);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to seed demo data: {Message}", ex.Message);
        // Don't throw - continue without demo data
    }
}

// Server-side API service implementations for Blazor Server
/// <summary>
/// Server-side implementation of ITeamApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls
/// </summary>
public class ServerSideTeamApiService : SereniTeam.Client.Services.ITeamApiService
{
    private readonly ITeamService _teamService;
    private readonly ILogger<ServerSideTeamApiService> _logger;

    public ServerSideTeamApiService(ITeamService teamService, ILogger<ServerSideTeamApiService> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task<List<SereniTeam.Shared.DTOs.TeamDto>> GetAllTeamsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting all teams");
            var result = await _teamService.GetAllTeamsAsync();
            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} teams", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting all teams");
            throw;
        }
    }

    public async Task<SereniTeam.Shared.DTOs.TeamDto?> GetTeamByIdAsync(int id)
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting team {TeamId}", id);
            var result = await _teamService.GetTeamByIdAsync(id);
            _logger.LogDebug("ServerSideTeamApiService: Retrieved team {TeamId}: {Found}", id, result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting team {TeamId}", id);
            throw;
        }
    }

    public async Task<SereniTeam.Shared.DTOs.TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting team summary for {TeamId}, {DaysBack} days", id, daysBack);
            var result = await _teamService.GetTeamSummaryAsync(id, daysBack);
            _logger.LogDebug("ServerSideTeamApiService: Retrieved team summary for {TeamId}: {Found}", id, result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting team summary for {TeamId}", id);
            throw;
        }
    }

    public async Task<int> CreateTeamAsync(SereniTeam.Shared.DTOs.CreateTeamDto team)
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Creating team {TeamName}", team.Name);
            var result = await _teamService.CreateTeamAsync(team);
            _logger.LogDebug("ServerSideTeamApiService: Created team {TeamName} with ID {TeamId}", team.Name, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error creating team {TeamName}", team.Name);
            throw;
        }
    }

    public async Task<List<SereniTeam.Shared.DTOs.BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting burnout alerts");
            var result = await _teamService.GetBurnoutAlertsAsync();
            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} burnout alerts", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting burnout alerts");
            throw;
        }
    }
}

/// <summary>
/// Server-side implementation of ICheckInApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls
/// </summary>
public class ServerSideCheckInApiService : SereniTeam.Client.Services.ICheckInApiService
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<ServerSideCheckInApiService> _logger;

    public ServerSideCheckInApiService(ICheckInService checkInService, ILogger<ServerSideCheckInApiService> logger)
    {
        _checkInService = checkInService;
        _logger = logger;
    }

    public async Task<bool> SubmitCheckInAsync(SereniTeam.Shared.DTOs.CheckInSubmissionDto checkIn)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Submitting check-in for team {TeamId}", checkIn.TeamId);
            var result = await _checkInService.SubmitCheckInAsync(checkIn);
            _logger.LogDebug("ServerSideCheckInApiService: Check-in submission result: {Success}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error submitting check-in for team {TeamId}", checkIn.TeamId);
            return false;
        }
    }
}
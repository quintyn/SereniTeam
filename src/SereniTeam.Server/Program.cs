using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SereniTeam.Client.Services;
using SereniTeam.Server.Data;
using SereniTeam.Server.Hubs;
using SereniTeam.Server.Services;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

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

// CRITICAL FIX: Use AddDbContextFactory instead of AddDbContext for Blazor Server
// This prevents the "second operation started" concurrency errors

// Force IPv4 to fix Azure App Service IPv6 connectivity issues with Supabase
var modifiedConnectionString = connectionString;
try
{
    // Extract host from connection string and resolve to IPv4
    var host = connectionString.Split(';')
        .FirstOrDefault(s => s.Trim().StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
        ?.Split('=')[1]?.Trim();

    if (!string.IsNullOrEmpty(host) && !System.Net.IPAddress.TryParse(host, out _))
    {
        var addresses = System.Net.Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (ipv4 != null)
        {
            modifiedConnectionString = connectionString.Replace($"Host={host}", $"Host={ipv4}");
            Console.WriteLine($"Resolved {host} to IPv4: {ipv4}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"IPv4 resolution failed, using original: {ex.Message}");
}

builder.Services.AddDbContextFactory<SereniTeamContext>(options =>
{
    options.UseNpgsql(modifiedConnectionString, npgsqlOptions =>
    {
        // Add retry logic for Azure/Supabase
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });

    // Enable detailed logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();

builder.Services.AddScoped<SereniTeam.Client.Services.ITeamApiService, ServerSideTeamApiService>(provider =>
{
    var contextFactory = provider.GetRequiredService<IDbContextFactory<SereniTeamContext>>();
    var logger = provider.GetRequiredService<ILogger<ServerSideTeamApiService>>();
    var hubContext = provider.GetRequiredService<IHubContext<TeamUpdatesHub>>();
    return new ServerSideTeamApiService(contextFactory, logger, hubContext);
});

builder.Services.AddScoped<SereniTeam.Client.Services.ICheckInApiService, ServerSideCheckInApiService>(provider =>
{
    var contextFactory = provider.GetRequiredService<IDbContextFactory<SereniTeamContext>>();
    var logger = provider.GetRequiredService<ILogger<ServerSideCheckInApiService>>();
    var hubContext = provider.GetRequiredService<IHubContext<TeamUpdatesHub>>();
    return new ServerSideCheckInApiService(contextFactory, logger, hubContext);
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

// Database health check (with error handling) - FIXED to use DbContextFactory
app.MapGet("/health/db", async (IDbContextFactory<SereniTeamContext> contextFactory) =>
{
    try
    {
        using var context = contextFactory.CreateDbContext();
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
app.MapBlazorHub(); // SignalR hub for Blazor Server
app.MapRazorPages(); // For _Host.cshtml
app.MapFallbackToPage("/_Host"); // Host page for Blazor Server

Console.WriteLine("=== Endpoint mapping complete ===");

// Database setup with improved error handling - FIXED to use DbContextFactory

// Check if database setup should be skipped
var skipDbSetup = app.Configuration.GetValue<bool>("SkipDatabaseSetup", false);
if (skipDbSetup)
{
    Console.WriteLine("Database setup skipped (SkipDatabaseSetup=true)");
}
else
{
    await SetupDatabase(app);
}

Console.WriteLine("=== SereniTeam Ready (Blazor Server) ===");
Console.WriteLine("Access points:");
Console.WriteLine("- Main App: /");
Console.WriteLine("- API Docs: /swagger");
Console.WriteLine("- Health: /health");
Console.WriteLine("- Database Health: /health/db");
Console.WriteLine("- File Debug: /debug/files");

await app.RunAsync();

/// <summary>
/// Database setup with comprehensive error handling - FIXED to use DbContextFactory
/// </summary>
static async Task SetupDatabase(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SereniTeamContext>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting database setup...");

        using var context = contextFactory.CreateDbContext();

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
/// Seeds demo data for the application - FIXED to work with passed context
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

// UPDATED: Server-side API service implementations for Blazor Server with DbContextFactory
/// <summary>
/// Server-side implementation of ITeamApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls - UPDATED for DbContextFactory
/// </summary>
public class ServerSideTeamApiService : SereniTeam.Client.Services.ITeamApiService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<ServerSideTeamApiService> _logger;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;

    public ServerSideTeamApiService(
        IDbContextFactory<SereniTeamContext> contextFactory,
        ILogger<ServerSideTeamApiService> logger,
        IHubContext<TeamUpdatesHub> hubContext)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<List<SereniTeam.Shared.DTOs.TeamDto>> GetAllTeamsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting all teams");

            using var context = _contextFactory.CreateDbContext();

            var teams = await context.Teams
                .Where(t => t.IsActive)
                .Select(t => new SereniTeam.Shared.DTOs.TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} teams", teams.Count);
            return teams;
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

            using var context = _contextFactory.CreateDbContext();

            var team = await context.Teams
                .Where(t => t.Id == id && t.IsActive)
                .Select(t => new SereniTeam.Shared.DTOs.TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive
                })
                .FirstOrDefaultAsync();

            _logger.LogDebug("ServerSideTeamApiService: Retrieved team {TeamId}: {Found}", id, team != null);
            return team;
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

            using var context = _contextFactory.CreateDbContext();

            var team = await context.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (team == null) return null;

            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            var checkIns = await context.CheckIns
                .Where(c => c.TeamId == id && c.SubmittedAt >= cutoffDate)
                .ToListAsync();

            // Calculate daily trends
            var dailyTrends = checkIns
                .GroupBy(c => c.SubmittedAt.Date)
                .Select(g => new DailyTrendDto
                {
                    Date = g.Key,
                    AverageMood = g.Average(c => c.MoodRating),
                    AverageStress = g.Average(c => c.StressLevel),
                    CheckInCount = g.Count()
                })
                .OrderByDescending(t => t.Date)
                .Take(30)
                .ToList();

            var summary = new SereniTeam.Shared.DTOs.TeamSummaryDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                Description = team.Description,
                AverageMood = checkIns.Any() ? checkIns.Average(c => c.MoodRating) : 0,
                AverageStress = checkIns.Any() ? checkIns.Average(c => c.StressLevel) : 0,
                TotalCheckIns = checkIns.Count,
                LastCheckInDate = checkIns.Any() ? checkIns.Max(c => c.SubmittedAt) : null,
                IsBurnoutRisk = CalculateBurnoutRisk(checkIns),
                RecentTrends = dailyTrends
            };

            _logger.LogDebug("ServerSideTeamApiService: Retrieved team summary for {TeamId}: {Found}", id, summary != null);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting team summary for {TeamId}", id);
            throw;
        }
    }

    public async Task<int> CreateTeamAsync(SereniTeam.Shared.DTOs.CreateTeamDto teamDto)
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Creating team {TeamName}", teamDto.Name);

            using var context = _contextFactory.CreateDbContext();

            var team = new SereniTeam.Shared.Models.Team
            {
                Name = teamDto.Name,
                Description = teamDto.Description,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Teams.Add(team);
            await context.SaveChangesAsync();

            _logger.LogDebug("ServerSideTeamApiService: Created team {TeamName} with ID {TeamId}", teamDto.Name, team.Id);
            return team.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error creating team {TeamName}", teamDto.Name);
            throw;
        }
    }

    public async Task<List<SereniTeam.Shared.DTOs.BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting burnout alerts");

            using var context = _contextFactory.CreateDbContext();

            var alerts = new List<SereniTeam.Shared.DTOs.BurnoutAlertDto>();
            var cutoffDate = DateTime.UtcNow.AddDays(-7); // Check last 7 days

            var teams = await context.Teams
                .Where(t => t.IsActive)
                .ToListAsync();

            foreach (var team in teams)
            {
                var recentCheckIns = await context.CheckIns
                    .Where(c => c.TeamId == team.Id && c.SubmittedAt >= cutoffDate)
                    .ToListAsync();

                if (recentCheckIns.Any())
                {
                    var avgMood = recentCheckIns.Average(c => c.MoodRating);
                    var avgStress = recentCheckIns.Average(c => c.StressLevel);

                    // Simple burnout detection logic
                    if (avgMood <= 3.0 || avgStress >= 8.0)
                    {
                        var severity = (avgMood <= 2.0 || avgStress >= 9.0) ? "High" : "Medium";

                        alerts.Add(new SereniTeam.Shared.DTOs.BurnoutAlertDto
                        {
                            TeamId = team.Id,
                            TeamName = team.Name,
                            AlertLevel = severity,
                            Message = $"Team showing signs of burnout - Avg Mood: {avgMood:F1}, Avg Stress: {avgStress:F1}",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} burnout alerts", alerts.Count);
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting burnout alerts");
            throw;
        }
    }

    private bool CalculateBurnoutRisk(List<SereniTeam.Shared.Models.CheckIn> checkIns)
    {
        if (!checkIns.Any()) return false;

        // Simple burnout risk calculation
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);

        // Risk if mood is low (≤3) or stress is high (≥8)
        return avgMood <= 3.0 || avgStress >= 8.0;
    }
}

/// <summary>
/// Server-side implementation of ICheckInApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls - UPDATED for DbContextFactory
/// </summary>
public class ServerSideCheckInApiService : ICheckInApiService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<ServerSideCheckInApiService> _logger;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;

    public ServerSideCheckInApiService(
        IDbContextFactory<SereniTeamContext> contextFactory,
        ILogger<ServerSideCheckInApiService> logger,
        IHubContext<TeamUpdatesHub> hubContext)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<bool> SubmitCheckInAsync(SereniTeam.Shared.DTOs.CheckInSubmissionDto checkIn)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Submitting check-in for team {TeamId}", checkIn.TeamId);

            using var context = _contextFactory.CreateDbContext();

            // Validate team exists
            var teamExists = await context.Teams
                .AnyAsync(t => t.Id == checkIn.TeamId && t.IsActive);

            if (!teamExists)
            {
                _logger.LogWarning("Attempted to submit check-in for non-existent team {TeamId}", checkIn.TeamId);
                return false;
            }

            var checkInEntity = new SereniTeam.Shared.Models.CheckIn
            {
                TeamId = checkIn.TeamId,
                MoodRating = checkIn.MoodRating,
                StressLevel = checkIn.StressLevel,
                Notes = checkIn.Notes,
                SubmittedAt = DateTime.UtcNow
            };

            context.CheckIns.Add(checkInEntity);
            await context.SaveChangesAsync();

            // Trigger SignalR notification
            await _hubContext.Clients.Group($"Team_{checkIn.TeamId}")
                .SendAsync("NewCheckInReceived", new CheckInDto
                {
                    Id = checkInEntity.Id,
                    TeamId = checkInEntity.TeamId,
                    MoodRating = checkInEntity.MoodRating,
                    StressLevel = checkInEntity.StressLevel,
                    Notes = checkInEntity.Notes,
                    SubmittedAt = checkInEntity.SubmittedAt
                });

            _logger.LogDebug("ServerSideCheckInApiService: Check-in submission successful for team {TeamId}", checkIn.TeamId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error submitting check-in for team {TeamId}", checkIn.TeamId);
            return false;
        }
    }

    public async Task<List<CheckInDto>> GetTeamCheckInsAsync(int teamId, int daysBack = 30)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Getting check-ins for team {TeamId}", teamId);

            using var context = _contextFactory.CreateDbContext();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            var checkIns = await context.CheckIns
                .Where(c => c.TeamId == teamId && c.SubmittedAt >= cutoffDate)
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new CheckInDto
                {
                    Id = c.Id,
                    TeamId = c.TeamId,
                    MoodRating = c.MoodRating,
                    StressLevel = c.StressLevel,
                    Notes = c.Notes,
                    SubmittedAt = c.SubmittedAt
                })
                .ToListAsync();

            _logger.LogDebug("ServerSideCheckInApiService: Retrieved {Count} check-ins for team {TeamId}", checkIns.Count, teamId);
            return checkIns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error getting check-ins for team {TeamId}", teamId);
            throw;
        }
    }
}
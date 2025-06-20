using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Moq;
using FluentAssertions;
using SereniTeam.Server.Data;
using SereniTeam.Server.Services;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;
using Xunit;

namespace SereniTeam.Tests.Services;

public class ServerSideTeamApiServiceTests : IDisposable
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly Mock<ILogger<ServerSideTeamApiService>> _mockLogger;
    private readonly Mock<IHubContext<TeamUpdatesHub>> _mockHubContext;
    private readonly ServerSideTeamApiService _teamService;

    public ServerSideTeamApiServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<SereniTeamContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Create a context factory for the in-memory database
        var mockFactory = new Mock<IDbContextFactory<SereniTeamContext>>();
        mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SereniTeamContext(options));
        _contextFactory = mockFactory.Object;

        _mockLogger = new Mock<ILogger<ServerSideTeamApiService>>();

        // Mock SignalR hub context - using the correct interface hierarchy
        _mockHubContext = new Mock<IHubContext<TeamUpdatesHub>>();
        var mockHubClients = new Mock<IHubClients>();
        var mockGroupManager = new Mock<IGroupManager>();
        var mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockHubClients.Object);
        _mockHubContext.Setup(x => x.Groups).Returns(mockGroupManager.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        _teamService = new ServerSideTeamApiService(_contextFactory, _mockLogger.Object, _mockHubContext.Object);
    }

    [Fact]
    public async Task GetAllTeamsAsync_ReturnsActiveTeamsOnly()
    {
        // Arrange
        using var context = _contextFactory.CreateDbContext();
        await context.Teams.AddRangeAsync(
            new Team { Id = 1, Name = "Active Team", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Team { Id = 2, Name = "Inactive Team", IsActive = false, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetAllTeamsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active Team");
        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTeamAsync_CreatesNewTeam_ReturnsId()
    {
        // Arrange
        var createTeamDto = new CreateTeamDto
        {
            Name = "Test Team",
            Description = "Test Description"
        };

        // Act
        var result = await _teamService.CreateTeamAsync(createTeamDto);

        // Assert
        result.Should().BeGreaterThan(0);

        using var context = _contextFactory.CreateDbContext();
        var createdTeam = await context.Teams.FindAsync(result);
        createdTeam.Should().NotBeNull();
        createdTeam!.Name.Should().Be("Test Team");
        createdTeam.Description.Should().Be("Test Description");
        createdTeam.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetTeamSummaryAsync_WithNoCheckIns_ReturnsEmptySummary()
    {
        // Arrange
        using var context = _contextFactory.CreateDbContext();
        var team = new Team { Id = 1, Name = "Empty Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await context.Teams.AddAsync(team);
        await context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetTeamSummaryAsync(1, 30);

        // Assert
        result.Should().NotBeNull();
        result!.TeamId.Should().Be(1);
        result.TeamName.Should().Be("Empty Team");
        result.AverageMood.Should().Be(0);
        result.AverageStress.Should().Be(0);
        result.TotalCheckIns.Should().Be(0);
        result.RecentTrends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTeamSummaryAsync_WithCheckIns_CalculatesCorrectAverages()
    {
        // Arrange
        using var context = _contextFactory.CreateDbContext();
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await context.Teams.AddAsync(team);

        var checkIns = new[]
        {
            new CheckIn { TeamId = 1, MoodRating = 8, StressLevel = 3, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 1, MoodRating = 6, StressLevel = 5, SubmittedAt = DateTime.UtcNow.AddDays(-2) },
            new CheckIn { TeamId = 1, MoodRating = 7, StressLevel = 4, SubmittedAt = DateTime.UtcNow.AddDays(-3) }
        };
        await context.CheckIns.AddRangeAsync(checkIns);
        await context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetTeamSummaryAsync(1, 30);

        // Assert
        result.Should().NotBeNull();
        result!.AverageMood.Should().BeApproximately(7.0, 0.1); // (8+6+7)/3 = 7
        result.AverageStress.Should().BeApproximately(4.0, 0.1); // (3+5+4)/3 = 4
        result.TotalCheckIns.Should().Be(3);
        result.RecentTrends.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(2, 8, true)]  // Low mood, high stress = burnout risk
    [InlineData(8, 2, false)] // High mood, low stress = no risk
    [InlineData(5, 5, false)] // Moderate levels = no risk
    public async Task BurnoutRiskCalculation_WorksCorrectly(int moodRating, int stressLevel, bool expectedRisk)
    {
        // Arrange
        using var context = _contextFactory.CreateDbContext();
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await context.Teams.AddAsync(team);

        // Add enough check-ins to trigger analysis
        var checkIns = new List<CheckIn>();
        for (int i = 0; i < 6; i++)
        {
            checkIns.Add(new CheckIn
            {
                TeamId = 1,
                MoodRating = moodRating,
                StressLevel = stressLevel,
                SubmittedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await context.CheckIns.AddRangeAsync(checkIns);
        await context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetTeamSummaryAsync(1, 30);

        // Assert
        result.Should().NotBeNull();
        result!.IsBurnoutRisk.Should().Be(expectedRisk);
    }

    [Fact]
    public async Task GetBurnoutAlertsAsync_ReturnsAlertsForAtRiskTeams()
    {
        // Arrange
        using var context = _contextFactory.CreateDbContext();
        var team1 = new Team { Id = 1, Name = "Healthy Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        var team2 = new Team { Id = 2, Name = "Stressed Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await context.Teams.AddRangeAsync(team1, team2);

        // Add healthy check-ins for team 1
        var healthyCheckIns = new[]
        {
            new CheckIn { TeamId = 1, MoodRating = 8, StressLevel = 3, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 1, MoodRating = 7, StressLevel = 4, SubmittedAt = DateTime.UtcNow.AddDays(-2) }
        };

        // Add stressed check-ins for team 2 (should trigger alert)
        var stressedCheckIns = new[]
        {
            new CheckIn { TeamId = 2, MoodRating = 2, StressLevel = 9, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 2, MoodRating = 1, StressLevel = 10, SubmittedAt = DateTime.UtcNow.AddDays(-2) }
        };

        await context.CheckIns.AddRangeAsync(healthyCheckIns.Concat(stressedCheckIns));
        await context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetBurnoutAlertsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().TeamId.Should().Be(2);
        result.First().TeamName.Should().Be("Stressed Team");
        result.First().AlertLevel.Should().Be("High");
    }

    public void Dispose()
    {
        // Context disposal is handled by the factory
    }
}
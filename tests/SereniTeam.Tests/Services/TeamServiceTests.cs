using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using SereniTeam.Server.Data;
using SereniTeam.Server.Services;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;
using Xunit;

namespace SereniTeam.Tests.Services;

public class TeamServiceTests : IDisposable
{
    private readonly SereniTeamContext _context;
    private readonly Mock<ILogger<TeamService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TeamService _teamService;

    public TeamServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<SereniTeamContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SereniTeamContext(options);
        _mockLogger = new Mock<ILogger<TeamService>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup default configuration values
        _mockConfiguration.Setup(x => x.GetValue<double>("SereniTeam:BurnoutThresholds:LowMoodThreshold", 3.0))
            .Returns(3.0);
        _mockConfiguration.Setup(x => x.GetValue<double>("SereniTeam:BurnoutThresholds:HighStressThreshold", 7.0))
            .Returns(7.0);
        _mockConfiguration.Setup(x => x.GetValue<int>("SereniTeam:BurnoutThresholds:ConsecutiveDaysForAlert", 3))
            .Returns(3);
        _mockConfiguration.Setup(x => x.GetValue<int>("SereniTeam:BurnoutThresholds:MinimumCheckInsForAnalysis", 5))
            .Returns(5);

        _teamService = new TeamService(_context, _mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task GetAllTeamsAsync_ReturnsActiveTeamsOnly()
    {
        // Arrange
        await _context.Teams.AddRangeAsync(
            new Team { Id = 1, Name = "Active Team", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Team { Id = 2, Name = "Inactive Team", IsActive = false, CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

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

        var createdTeam = await _context.Teams.FindAsync(result);
        createdTeam.Should().NotBeNull();
        createdTeam!.Name.Should().Be("Test Team");
        createdTeam.Description.Should().Be("Test Description");
        createdTeam.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetTeamSummaryAsync_WithNoCheckIns_ReturnsEmptySummary()
    {
        // Arrange
        var team = new Team { Id = 1, Name = "Empty Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

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
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);

        var checkIns = new[]
        {
            new CheckIn { TeamId = 1, MoodRating = 8, StressLevel = 3, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 1, MoodRating = 6, StressLevel = 5, SubmittedAt = DateTime.UtcNow.AddDays(-2) },
            new CheckIn { TeamId = 1, MoodRating = 7, StressLevel = 4, SubmittedAt = DateTime.UtcNow.AddDays(-3) }
        };
        await _context.CheckIns.AddRangeAsync(checkIns);
        await _context.SaveChangesAsync();

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
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);

        // Add enough check-ins over consecutive days to trigger analysis
        var checkIns = new List<CheckIn>();
        for (int i = 0; i < 6; i++) // 6 check-ins over 3 days
        {
            checkIns.Add(new CheckIn
            {
                TeamId = 1,
                MoodRating = moodRating,
                StressLevel = stressLevel,
                SubmittedAt = DateTime.UtcNow.AddDays(-i / 2) // 2 per day
            });
        }

        await _context.CheckIns.AddRangeAsync(checkIns);
        await _context.SaveChangesAsync();

        // Act
        var result = await _teamService.GetTeamSummaryAsync(1, 30);

        // Assert
        result.Should().NotBeNull();
        result!.IsBurnoutRisk.Should().Be(expectedRisk);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
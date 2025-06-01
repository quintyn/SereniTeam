using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Moq;
using FluentAssertions;
using SereniTeam.Server.Data;
using SereniTeam.Server.Services;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.Models;
using SereniTeam.Shared.DTOs;
using Xunit;

namespace SereniTeam.Tests.Services;

public class CheckInServiceTests : IDisposable
{
    private readonly SereniTeamContext _context;
    private readonly Mock<ILogger<CheckInService>> _mockLogger;
    private readonly Mock<IHubContext<TeamUpdatesHub>> _mockHubContext;
    private readonly CheckInService _checkInService;

    public CheckInServiceTests()
    {
        var options = new DbContextOptionsBuilder<SereniTeamContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SereniTeamContext(options);
        _mockLogger = new Mock<ILogger<CheckInService>>();

        // Mock SignalR hub context
        _mockHubContext = new Mock<IHubContext<TeamUpdatesHub>>();
        var mockClients = new Mock<IHubCallerClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        _checkInService = new CheckInService(_context, _mockHubContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SubmitCheckInAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        var checkInDto = new CheckInSubmissionDto
        {
            TeamId = 1,
            MoodRating = 7,
            StressLevel = 4,
            Notes = "Feeling good today"
        };

        // Act
        var result = await _checkInService.SubmitCheckInAsync(checkInDto);

        // Assert
        result.Should().BeTrue();

        var savedCheckIn = await _context.CheckIns.FirstOrDefaultAsync();
        savedCheckIn.Should().NotBeNull();
        savedCheckIn!.TeamId.Should().Be(1);
        savedCheckIn.MoodRating.Should().Be(7);
        savedCheckIn.StressLevel.Should().Be(4);
        savedCheckIn.Notes.Should().Be("Feeling good today");
    }

    [Fact]
    public async Task SubmitCheckInAsync_WithInvalidTeam_ReturnsFalse()
    {
        // Arrange
        var checkInDto = new CheckInSubmissionDto
        {
            TeamId = 999, // Non-existent team
            MoodRating = 7,
            StressLevel = 4
        };

        // Act
        var result = await _checkInService.SubmitCheckInAsync(checkInDto);

        // Assert
        result.Should().BeFalse();
        var checkIns = await _context.CheckIns.ToListAsync();
        checkIns.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitCheckInAsync_CallsSignalRHub()
    {
        // Arrange
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        var checkInDto = new CheckInSubmissionDto
        {
            TeamId = 1,
            MoodRating = 7,
            StressLevel = 4
        };

        // Act
        await _checkInService.SubmitCheckInAsync(checkInDto);

        // Assert
        _mockHubContext.Verify(
            x => x.Clients.Group("Team_1").SendAsync(
                "NewCheckInReceived",
                It.IsAny<object>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetTeamCheckInsAsync_ReturnsCorrectCheckIns()
    {
        // Arrange
        var team1 = new Team { Id = 1, Name = "Team 1", IsActive = true, CreatedAt = DateTime.UtcNow };
        var team2 = new Team { Id = 2, Name = "Team 2", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddRangeAsync(team1, team2);

        var checkIns = new[]
        {
            new CheckIn { TeamId = 1, MoodRating = 8, StressLevel = 3, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 1, MoodRating = 6, StressLevel = 5, SubmittedAt = DateTime.UtcNow.AddDays(-2) },
            new CheckIn { TeamId = 2, MoodRating = 7, StressLevel = 4, SubmittedAt = DateTime.UtcNow.AddDays(-1) }
        };
        await _context.CheckIns.AddRangeAsync(checkIns);
        await _context.SaveChangesAsync();

        // Act
        var result = await _checkInService.GetTeamCheckInsAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.TeamId == 1);

        // Should be ordered by most recent first
        result[0].SubmittedAt.Should().BeAfter(result[1].SubmittedAt);
    }

    [Fact]
    public async Task GetTeamCheckInsAsync_WithDateFilter_ReturnsFilteredResults()
    {
        // Arrange
        var team = new Team { Id = 1, Name = "Test Team", IsActive = true, CreatedAt = DateTime.UtcNow };
        await _context.Teams.AddAsync(team);

        var checkIns = new[]
        {
            new CheckIn { TeamId = 1, MoodRating = 8, StressLevel = 3, SubmittedAt = DateTime.UtcNow.AddDays(-1) },
            new CheckIn { TeamId = 1, MoodRating = 6, StressLevel = 5, SubmittedAt = DateTime.UtcNow.AddDays(-10) }
        };
        await _context.CheckIns.AddRangeAsync(checkIns);
        await _context.SaveChangesAsync();

        // Act
        var result = await _checkInService.GetTeamCheckInsAsync(1, DateTime.UtcNow.AddDays(-5));

        // Assert
        result.Should().HaveCount(1);
        result[0].MoodRating.Should().Be(8);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
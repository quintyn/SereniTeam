using System.Net.Http.Json;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Client.Services;

/// <summary>
/// Service for team-related API operations
/// </summary>
public class TeamApiService : ITeamApiService
{
    private readonly HttpClient _httpClient;

    public TeamApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TeamDto>> GetAllTeamsAsync()
    {
        try
        {
            var teams = await _httpClient.GetFromJsonAsync<List<TeamDto>>("api/teams");
            return teams ?? new List<TeamDto>();
        }
        catch (HttpRequestException)
        {
            return new List<TeamDto>();
        }
    }

    public async Task<TeamDto?> GetTeamByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TeamDto>($"api/teams/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TeamSummaryDto>($"api/teams/{id}/summary?daysBack={daysBack}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<int> CreateTeamAsync(CreateTeamDto team)
    {
        var response = await _httpClient.PostAsJsonAsync("api/teams", team);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
            var alerts = await _httpClient.GetFromJsonAsync<List<BurnoutAlertDto>>("api/teams/alerts");
            return alerts ?? new List<BurnoutAlertDto>();
        }
        catch (HttpRequestException)
        {
            return new List<BurnoutAlertDto>();
        }
    }
}
using System.Net.Http.Json;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Client.Services;

/// <summary>
/// Service for check-in related API operations
/// </summary>
public class CheckInApiService : ICheckInApiService
{
    private readonly HttpClient _httpClient;

    public CheckInApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/checkins", checkIn);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
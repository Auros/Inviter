using Inviter.Server.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Inviter.Server.Services;

public interface ISoriginService
{
    Task<SoriginUser?> GetSoriginUser(string token);
}

internal class SoriginService : ISoriginService
{
    private readonly ILogger _logger;
    private readonly Version _version;
    private readonly HttpClient _httpClient;
    private readonly InviterSettings _inviterSettings;

    public SoriginService(ILogger<SoriginService> logger, Version version, HttpClient httpClient, InviterSettings inviterSettings)
    {
        _logger = logger;
        _version = version;
        _httpClient = httpClient;
        _inviterSettings = inviterSettings;
    }

    public async Task<SoriginUser?> GetSoriginUser(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpRequestMessage request = new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_inviterSettings.SoriginURL}/api/users/@me"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(nameof(Inviter), _version.ToString()));

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unable to get Sorigin user from token.");
            return null;
        }

        string soriginUserJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SoriginUser>(soriginUserJson);
    }
}
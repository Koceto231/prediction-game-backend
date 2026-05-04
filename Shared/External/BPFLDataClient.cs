using BPFL.API.Exceptions;
using System.Net;
using System.Text.Json;

namespace BPFL.API.Shared.External
{
    public class BPFLDataClient
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<BPFLDataClient> _logger;

        public BPFLDataClient(HttpClient _httpClient, ILogger<BPFLDataClient> logger)
        {
            httpClient = _httpClient;
            _logger = logger;
        }

        private async Task<T> GetAsync<T>(string endpoint, CancellationToken ct) where T : class
        {
            _logger.LogDebug("Fetching data from: {Endpoint}", endpoint);
            try
            {
                var response = await httpClient.GetAsync(endpoint, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("API request failed: {StatusCode} - {Endpoint} - {Error}",
                        response.StatusCode, endpoint, errorContent);
                    throw new BPFLDataClientException($"API request failed: {errorContent}", response.StatusCode);
                }

                var content = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<T>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                    throw new BPFLDataClientException("Failed to deserialize response", response.StatusCode);

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
                throw new BPFLDataClientException($"Connection failed: {ex.Message}", HttpStatusCode.ServiceUnavailable);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON returned from endpoint: {Endpoint}", endpoint);
                throw new BPFLDataClientException("Invalid JSON returned from external API.", HttpStatusCode.BadGateway);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Timeout while calling external API: {Endpoint}", endpoint);
                throw new BPFLDataClientException(
                    "External API timeout. The football data service is too slow or unavailable.",
                    HttpStatusCode.GatewayTimeout);
            }
        }

        public async Task<CompetitionResponseDto> GetCompetions(CancellationToken ct)
            => await GetAsync<CompetitionResponseDto>("competitions", ct);

        public async Task<TeamResponseDto> GetTeamAsync(string idOrCode, CancellationToken ct)
            => await GetAsync<TeamResponseDto>($"competitions/{idOrCode}/teams", ct);

        public async Task<MatchesResponseDTO> GetAllMatchesAsync(string leagueCode, CancellationToken ct)
            => await GetAsync<MatchesResponseDTO>($"competitions/{leagueCode}/matches", ct);

        public async Task<ExternalTeamDTO?> GetSingleTeamAsync(int teamId, CancellationToken ct)
        {
            try { return await GetAsync<ExternalTeamDTO>($"teams/{teamId}", ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch team {Id}", teamId); return null; }
        }

        public async Task<ExternalMatchDetailDTO?> GetMatchDetailAsync(int externalMatchId, CancellationToken ct)
        {
            try { return await GetAsync<ExternalMatchDetailDTO>($"matches/{externalMatchId}", ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch match detail for externalId={Id}", externalMatchId); return null; }
        }
    }
}

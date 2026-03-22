using BPFL.API.Exceptions;
using BPFL.API.Models.ExternalDto;
using System.Net;
using System.Text.Json;

namespace BPFL.API.Services.External
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
                        response.StatusCode,
                        endpoint,
                        errorContent);

                    throw new BPFLDataClientException($"API request failed: {errorContent}", response.StatusCode);
                }

                var content = await response.Content.ReadAsStringAsync(ct);

                Console.WriteLine("RAW API RESPONSE:");
                Console.WriteLine(content);

                var result = JsonSerializer.Deserialize<T>(content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (result == null)
                {
          
                    throw new BPFLDataClientException("Failed to deserialize response", response.StatusCode);
                }

                return result;
            } 
            catch(HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
                throw new BPFLDataClientException($"Connection failed: {ex.Message}", HttpStatusCode.ServiceUnavailable);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON returned from endpoint: {Endpoint}", endpoint);
                
                throw new BPFLDataClientException(
                   "Invalid JSON returned from external API.",
                    HttpStatusCode.BadGateway);
                
               // return Activator.CreateInstance<T>();
            }
        }
        public async Task<CompetitionResponseDto> GetCompetions(CancellationToken ct)
        {
            return await GetAsync<CompetitionResponseDto>("competitions", ct);
            
        }

        public async Task<TeamResponseDto> GetTeamAsync(string idOrCode, CancellationToken ct)
        {
            return await GetAsync<TeamResponseDto>($"competitions/{idOrCode}/teams", ct);

        }
        public async Task<MatchesResponseDTO> GetAllMatchesAsync(string leagueCode, CancellationToken ct)
        {
            return await GetAsync<MatchesResponseDTO>($"competitions/{leagueCode}/matches", ct);
        }
    }
}

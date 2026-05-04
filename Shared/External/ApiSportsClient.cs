using System.Text.Json;
using System.Text.Json.Serialization;

namespace BPFL.API.Shared.External
{
    public class ApiSportsClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<ApiSportsClient> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ApiSportsClient(HttpClient http, IConfiguration config, ILogger<ApiSportsClient> logger)
        {
            _http = http;
            _logger = logger;

            var key = config["ApiSports:ApiKey"];
            if (string.IsNullOrWhiteSpace(key))
                _logger.LogWarning("ApiSports:ApiKey is not configured — api-sports calls will fail.");
            _http.BaseAddress = new Uri("https://v3.football.api-sports.io/");
            if (!string.IsNullOrWhiteSpace(key))
                _http.DefaultRequestHeaders.Add("x-apisports-key", key);
        }

        public async Task<List<ApiSportsFixtureResponse>> GetFixturesByDateAsync(
            DateOnly date, int leagueId, int season, CancellationToken ct = default)
        {
            var root = await GetAsync<ApiSportsRoot<ApiSportsFixtureResponse>>(
                $"fixtures?date={date:yyyy-MM-dd}&league={leagueId}&season={season}", ct);
            return root?.Response ?? [];
        }

        public async Task<List<ApiSportsTeamPlayerStats>> GetFixturePlayersAsync(
            int fixtureId, CancellationToken ct = default)
        {
            var root = await GetAsync<ApiSportsRoot<ApiSportsTeamPlayerStats>>(
                $"fixtures/players?fixture={fixtureId}", ct);
            return root?.Response ?? [];
        }

        public async Task<List<ApiSportsTeamEntry>> GetTeamsByLeagueAsync(
            int leagueId, int season, CancellationToken ct = default)
        {
            var root = await GetAsync<ApiSportsRoot<ApiSportsTeamEntry>>(
                $"teams?league={leagueId}&season={season}", ct);
            return root?.Response ?? [];
        }

        public async Task<List<ApiSportsSquadPlayer>> GetSquadAsync(
            int teamId, CancellationToken ct = default)
        {
            var root = await GetAsync<ApiSportsRoot<ApiSportsSquadResponse>>(
                $"players/squads?team={teamId}", ct);
            return root?.Response?.FirstOrDefault()?.Players ?? [];
        }

        public async Task<int> GetRemainingRequestsAsync(CancellationToken ct = default)
        {
            var resp = await _http.GetAsync("status", ct);
            if (!resp.IsSuccessStatusCode) return 0;
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("response")
                .GetProperty("requests")
                .GetProperty("current") is var cur
                ? 100 - cur.GetInt32()
                : 0;
        }

        private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("ApiSports GET {BaseUrl}{Url}", _http.BaseAddress, url);
                var resp = await _http.GetAsync(url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ApiSports {Url} returned {Status}: {Body}", url, resp.StatusCode, body);
                    return default;
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind != JsonValueKind.Array ||
                    (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) ||
                    (errors.ValueKind == JsonValueKind.Object && errors.EnumerateObject().Any()))
                {
                    _logger.LogWarning("ApiSports errors in {Url}: {Errors}", url, errors.ToString());
                }

                return JsonSerializer.Deserialize<T>(body, _json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApiSports request failed: {Url}", url);
                return default;
            }
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────

    public class ApiSportsRoot<T> { public List<T> Response { get; set; } = []; }

    public class ApiSportsFixtureResponse
    {
        public ApiSportsFixture Fixture { get; set; } = null!;
        public ApiSportsTeams Teams { get; set; } = null!;
    }

    public class ApiSportsFixture
    {
        public int Id { get; set; }
        public string? Date { get; set; }
    }

    public class ApiSportsTeams
    {
        public ApiSportsTeamInfo Home { get; set; } = null!;
        public ApiSportsTeamInfo Away { get; set; } = null!;
    }

    public class ApiSportsTeamInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class ApiSportsTeamPlayerStats
    {
        public ApiSportsTeamInfo Team { get; set; } = null!;
        public List<ApiSportsPlayerStatEntry> Players { get; set; } = [];
    }

    public class ApiSportsPlayerStatEntry
    {
        public ApiSportsPlayerInfo Player { get; set; } = null!;
        public List<ApiSportsStatistics> Statistics { get; set; } = [];
    }

    public class ApiSportsPlayerInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Photo { get; set; }
    }

    public class ApiSportsStatistics
    {
        public ApiSportsGames Games { get; set; } = null!;
        public ApiSportsGoals Goals { get; set; } = null!;
        public ApiSportsCards Cards { get; set; } = null!;
    }

    public class ApiSportsGames
    {
        public int? Minutes { get; set; }
        public string? Position { get; set; }
    }

    public class ApiSportsGoals
    {
        public int? Total { get; set; }
        public int? Assists { get; set; }
        public int? Conceded { get; set; }
    }

    public class ApiSportsCards
    {
        public int? Yellow { get; set; }
        public int? Red { get; set; }
    }

    public class ApiSportsTeamEntry { public ApiSportsTeamInfo Team { get; set; } = null!; }

    public class ApiSportsSquadResponse
    {
        public ApiSportsTeamInfo Team { get; set; } = null!;
        public List<ApiSportsSquadPlayer> Players { get; set; } = [];
    }

    public class ApiSportsSquadPlayer
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int? Age { get; set; }
        public string? Position { get; set; }
        public string? Photo { get; set; }
    }
}

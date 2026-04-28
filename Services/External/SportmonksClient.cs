using System.Text.Json;
using System.Text.Json.Serialization;

namespace BPFL.API.Services.External
{
    public class SportmonksClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<SportmonksClient> _logger;

        // Leagues available on our Starter plan
        public static readonly Dictionary<string, int> LeagueMap = new()
        {
            ["PL"]  = 8,    // Premier League
            ["BL1"] = 82,   // Bundesliga
            ["BGL"] = 229,  // efbet Liga (Bulgarian First League)
            ["SA"]  = 384,  // Serie A
            ["PD"]  = 564,  // La Liga
        };

        // Sportmonks event type IDs
        public static class EventType
        {
            public const int Goal       = 14;
            public const int OwnGoal    = 15;
            public const int YellowCard = 19;
            public const int RedCard    = 20;
        }

        // Sportmonks stat type IDs
        public const int StatCorners = 34;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public SportmonksClient(HttpClient http, IConfiguration config, ILogger<SportmonksClient> logger)
        {
            _http = http;
            _logger = logger;
            var key = config["Sportmonks:ApiKey"] ?? throw new InvalidOperationException("Sportmonks:ApiKey missing");
            _http.BaseAddress = new Uri("https://api.sportmonks.com/v3/football/");
            _http.DefaultRequestHeaders.Add("Authorization", key);
        }

        // ── Fixtures by date ──────────────────────────────────────────

        public async Task<List<SmFixture>> GetFixturesByDateAsync(
            DateOnly date, int[]? leagueIds = null, CancellationToken ct = default)
        {
            var url = $"fixtures/date/{date:yyyy-MM-dd}?include=participants;scores&per_page=100";
            var root = await GetAsync<SmRoot<SmFixture>>(url, ct);
            var all = root?.Data ?? [];
            if (leagueIds is { Length: > 0 })
                all = all.Where(f => leagueIds.Contains(f.LeagueId)).ToList();
            return all;
        }

        // ── Events for a fixture (goals, cards) ───────────────────────

        public async Task<List<SmEvent>> GetFixtureEventsAsync(
            int fixtureId, CancellationToken ct = default)
        {
            var root = await GetAsync<SmSingleRoot<SmFixtureWithIncludes>>(
                $"fixtures/{fixtureId}?include=events", ct);
            return root?.Data?.Events ?? [];
        }

        // ── Statistics for a fixture (corners etc.) ───────────────────

        public async Task<List<SmStatistic>> GetFixtureStatisticsAsync(
            int fixtureId, CancellationToken ct = default)
        {
            var root = await GetAsync<SmSingleRoot<SmFixtureWithIncludes>>(
                $"fixtures/{fixtureId}?include=statistics", ct);
            return root?.Data?.Statistics ?? [];
        }

        // ── Squad for a team ──────────────────────────────────────────

        public async Task<List<SmSquadPlayer>> GetSquadByTeamIdAsync(
            int teamId, CancellationToken ct = default)
        {
            var root = await GetAsync<SmRoot<SmSquadPlayer>>(
                $"squads/teams/{teamId}?include=player", ct);
            return root?.Data ?? [];
        }

        // ── Generic GET ───────────────────────────────────────────────

        private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Sportmonks GET {Url}", url);
                var resp = await _http.GetAsync(url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Sportmonks {Url} → {Status}: {Body}",
                        url, resp.StatusCode, body[..Math.Min(400, body.Length)]);
                    return default;
                }

                return JsonSerializer.Deserialize<T>(body, _json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sportmonks request failed: {Url}", url);
                return default;
            }
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────

    public class SmRoot<T>
    {
        public List<T> Data { get; set; } = [];
    }

    public class SmSingleRoot<T>
    {
        public T? Data { get; set; }
    }

    public class SmFixture
    {
        public int Id { get; set; }

        [JsonPropertyName("league_id")]
        public int LeagueId { get; set; }

        [JsonPropertyName("season_id")]
        public int SeasonId { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("starting_at")]
        public string? StartingAt { get; set; }

        [JsonPropertyName("state_id")]
        public int? StateId { get; set; }

        [JsonPropertyName("result_info")]
        public string? ResultInfo { get; set; }

        [JsonPropertyName("has_odds")]
        public bool HasOdds { get; set; }

        public List<SmParticipant> Participants { get; set; } = [];
        public List<SmScore> Scores { get; set; } = [];
    }

    public class SmParticipant
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        [JsonPropertyName("image_path")]
        public string? ImagePath { get; set; }

        public SmParticipantMeta? Meta { get; set; }
    }

    public class SmParticipantMeta
    {
        public string? Location { get; set; }  // "home" | "away"
        public bool? Winner { get; set; }
    }

    public class SmScore
    {
        [JsonPropertyName("participant_id")]
        public int ParticipantId { get; set; }

        public SmScoreValue? Score { get; set; }
        public string? Description { get; set; }
    }

    public class SmScoreValue
    {
        public int Goals { get; set; }
        public string? Participant { get; set; }
    }

    public class SmFixtureWithIncludes
    {
        public int Id { get; set; }
        public List<SmEvent> Events { get; set; } = [];
        public List<SmStatistic> Statistics { get; set; } = [];
    }

    public class SmEvent
    {
        public int Id { get; set; }

        [JsonPropertyName("type_id")]
        public int TypeId { get; set; }

        [JsonPropertyName("player_id")]
        public int? PlayerId { get; set; }

        [JsonPropertyName("player_name")]
        public string? PlayerName { get; set; }

        [JsonPropertyName("related_player_id")]
        public int? RelatedPlayerId { get; set; }

        [JsonPropertyName("related_player_name")]
        public string? RelatedPlayerName { get; set; }

        [JsonPropertyName("participant_id")]
        public int ParticipantId { get; set; }

        public int? Minute { get; set; }
    }

    public class SmStatistic
    {
        [JsonPropertyName("type_id")]
        public int TypeId { get; set; }

        [JsonPropertyName("participant_id")]
        public int ParticipantId { get; set; }

        public SmStatData? Data { get; set; }
        public string? Location { get; set; }
    }

    public class SmStatData
    {
        public int? Value { get; set; }
    }

    public class SmSquadPlayer
    {
        [JsonPropertyName("player_id")]
        public int PlayerId { get; set; }

        [JsonPropertyName("position_id")]
        public int? PositionId { get; set; }

        [JsonPropertyName("team_id")]
        public int TeamId { get; set; }

        public SmPlayerDetail? Player { get; set; }
    }

    public class SmPlayerDetail
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("common_name")]
        public string? CommonName { get; set; }
    }
}

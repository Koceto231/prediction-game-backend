using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Services.External;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services.FantasyServices
{
    public class ApiSportsPlayerSeedService
    {
        private readonly BPFL_DBContext _db;
        private readonly ApiSportsClient _apiSports;
        private readonly ILogger<ApiSportsPlayerSeedService> _logger;

        // api-sports league IDs for the leagues we track
        public static readonly Dictionary<string, int> LeagueMap = new()
        {
            ["PL"]  = 39,   // Premier League
            ["PD"]  = 140,  // La Liga
            ["SA"]  = 135,  // Serie A
            ["BL1"] = 78,   // Bundesliga
            ["FL1"] = 61,   // Ligue 1
            ["CL"]  = 2,    // Champions League
        };

        public ApiSportsPlayerSeedService(
            BPFL_DBContext db,
            ApiSportsClient apiSports,
            ILogger<ApiSportsPlayerSeedService> logger)
        {
            _db = db;
            _apiSports = apiSports;
            _logger = logger;
        }

        public async Task<SeedResult> SeedLeagueAsync(string leagueCode, int season, CancellationToken ct = default)
        {
            if (!LeagueMap.TryGetValue(leagueCode.ToUpper(), out var apiLeagueId))
                return new SeedResult(false, $"Unknown league code: {leagueCode}. Valid: {string.Join(", ", LeagueMap.Keys)}");

            // Get all teams in this league from api-sports
            var apiTeams = await _apiSports.GetTeamsByLeagueAsync(apiLeagueId, season, ct);
            if (apiTeams.Count == 0)
                return new SeedResult(false, "No teams returned from api-sports for this league/season.");

            _logger.LogInformation("Seeding {League} — {Count} teams", leagueCode, apiTeams.Count);

            int playersAdded = 0;
            int playersUpdated = 0;

            foreach (var apiTeam in apiTeams)
            {
                if (ct.IsCancellationRequested) break;

                // Find matching team in our DB by name (fuzzy)
                var dbTeam = await FindTeamAsync(apiTeam.Team.Name, ct);
                if (dbTeam == null)
                {
                    _logger.LogWarning("No DB team found for api-sports team: {Name}", apiTeam.Team.Name);
                    continue;
                }

                // Get squad from api-sports (1 request per team)
                var squad = await _apiSports.GetSquadAsync(apiTeam.Team.Id, ct);
                if (squad.Count == 0)
                {
                    _logger.LogWarning("Empty squad for {Team}", apiTeam.Team.Name);
                    continue;
                }

                foreach (var apiPlayer in squad)
                {
                    var pos = MapPosition(apiPlayer.Position);
                    var price = DefaultPrice(pos);

                    // Check if player already exists by name + team
                    var existing = await _db.FantasyPlayers
                        .FirstOrDefaultAsync(p => p.Name == apiPlayer.Name && p.TeamId == dbTeam.Id, ct);

                    if (existing != null)
                    {
                        existing.Position = pos;
                        existing.LastUpdatedAt = DateTime.UtcNow;
                        playersUpdated++;
                    }
                    else
                    {
                        _db.FantasyPlayers.Add(new FantasyPlayer
                        {
                            ExternalPlayerId = 0,
                            Name             = apiPlayer.Name,
                            Position         = pos,
                            TeamId           = dbTeam.Id,
                            Price            = price,
                            IsActive         = true,
                            CreatedAt        = DateTime.UtcNow,
                            LastUpdatedAt    = DateTime.UtcNow,
                        });
                        playersAdded++;
                    }
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Team {Team}: +{Added} players", apiTeam.Team.Name, squad.Count);

                // Small delay to be kind to the API
                await Task.Delay(200, ct);
            }

            return new SeedResult(true,
                $"Done. Added: {playersAdded}, Updated: {playersUpdated}, Teams processed: {apiTeams.Count}");
        }

        private async Task<Team?> FindTeamAsync(string apiName, CancellationToken ct)
        {
            var teams = await _db.Teams.AsNoTracking().ToListAsync(ct);
            // Exact match first
            var match = teams.FirstOrDefault(t =>
                string.Equals(t.Name, apiName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
            // Contains match
            return teams.FirstOrDefault(t =>
                t.Name.Contains(apiName, StringComparison.OrdinalIgnoreCase) ||
                apiName.Contains(t.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static FantasyPlayer.FantasyPosition MapPosition(string? pos) =>
            pos?.ToLower() switch
            {
                "goalkeeper" => FantasyPlayer.FantasyPosition.GK,
                "defender"   => FantasyPlayer.FantasyPosition.DEF,
                "midfielder" => FantasyPlayer.FantasyPosition.MID,
                "attacker"   => FantasyPlayer.FantasyPosition.FWD,
                _            => FantasyPlayer.FantasyPosition.MID
            };

        private static decimal DefaultPrice(FantasyPlayer.FantasyPosition pos) => pos switch
        {
            FantasyPlayer.FantasyPosition.GK  => 5.0m,
            FantasyPlayer.FantasyPosition.DEF => 5.0m,
            FantasyPlayer.FantasyPosition.MID => 6.5m,
            FantasyPlayer.FantasyPosition.FWD => 7.5m,
            _ => 5.0m
        };
    }

    public record SeedResult(bool Success, string Message);
}

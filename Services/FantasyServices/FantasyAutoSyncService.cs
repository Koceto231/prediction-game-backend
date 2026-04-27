using BPFL.API.Data;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Services.External;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services.FantasyServices
{
    public class FantasyAutoSyncService
    {
        private readonly BPFL_DBContext _db;
        private readonly BPFLDataClient _dataClient;
        private readonly ApiSportsClient _apiSports;
        private readonly FantasyServices _fantasyServices;
        private readonly ILogger<FantasyAutoSyncService> _logger;

        public FantasyAutoSyncService(
            BPFL_DBContext db,
            BPFLDataClient dataClient,
            ApiSportsClient apiSports,
            FantasyServices fantasyServices,
            ILogger<FantasyAutoSyncService> logger)
        {
            _db = db;
            _dataClient = dataClient;
            _apiSports = apiSports;
            _fantasyServices = fantasyServices;
            _logger = logger;
        }

        // ── Position mapping ──────────────────────────────────────────

        private static FantasyPlayer.FantasyPosition MapPosition(string? pos) =>
            pos?.ToLower() switch
            {
                "goalkeeper"                                                    => FantasyPlayer.FantasyPosition.GK,
                "centre-back" or "left-back" or "right-back"
                    or "left centre-back" or "right centre-back"
                    or "defence"                                                => FantasyPlayer.FantasyPosition.DEF,
                "defensive midfield" or "central midfield"
                    or "left midfield" or "right midfield" or "midfield"       => FantasyPlayer.FantasyPosition.MID,
                "attacking midfield" or "left winger" or "right winger"
                    or "centre-forward" or "second striker"
                    or "left wing" or "right wing" or "offence"                => FantasyPlayer.FantasyPosition.FWD,
                _ => FantasyPlayer.FantasyPosition.MID
            };

        private static decimal DefaultPrice(FantasyPlayer.FantasyPosition pos) => pos switch
        {
            FantasyPlayer.FantasyPosition.GK  => 5.0m,
            FantasyPlayer.FantasyPosition.DEF => 5.0m,
            FantasyPlayer.FantasyPosition.MID => 6.5m,
            FantasyPlayer.FantasyPosition.FWD => 7.5m,
            _ => 5.0m
        };

        // ── Player sync from squads ───────────────────────────────────

        /// <summary>
        /// Fetch squad data for every team in the DB that has an ExternalId.
        /// Calls /teams/{id} individually to guarantee squad data is returned.
        /// Safe to call repeatedly — idempotent via ExternalPlayerId.
        /// </summary>
        public async Task SyncPlayersFromSquadsAsync(string[] leagueCodes, CancellationToken ct = default)
        {
            // All DB teams with a known football-data.org ID
            var dbTeams = await _db.Teams.AsNoTracking()
                .Where(t => t.ExternalId > 0)
                .ToListAsync(ct);

            if (dbTeams.Count == 0)
            {
                _logger.LogWarning("No teams with ExternalId found — skipping fantasy player sync.");
                return;
            }

            var existingPlayers = await _db.FantasyPlayers
                .ToDictionaryAsync(p => p.ExternalPlayerId, p => p, ct);

            int added = 0, updated = 0, callCount = 0;

            foreach (var dbTeam in dbTeams)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // football-data.org free tier: 10 req/min → wait 7 s between calls
                    if (callCount > 0)
                        await Task.Delay(TimeSpan.FromSeconds(7), ct);

                    callCount++;
                    _logger.LogInformation("Fetching squad for team {Name} (ext={ExtId})", dbTeam.Name, dbTeam.ExternalId);

                    var teamDetail = await _dataClient.GetSingleTeamAsync(dbTeam.ExternalId, ct);
                    if (teamDetail?.Squad == null || teamDetail.Squad.Count == 0)
                    {
                        _logger.LogWarning("No squad returned for team {Name}", dbTeam.Name);
                        continue;
                    }

                    foreach (var squadPlayer in teamDetail.Squad)
                    {
                        if (squadPlayer.Id <= 0) continue;

                        var pos = MapPosition(squadPlayer.Position);

                        if (existingPlayers.TryGetValue(squadPlayer.Id, out var existing))
                        {
                            existing.Name          = squadPlayer.Name;
                            existing.Position      = pos;
                            existing.TeamId        = dbTeam.Id;
                            existing.IsActive      = true;
                            existing.LastUpdatedAt = DateTime.UtcNow;
                            updated++;
                        }
                        else
                        {
                            var player = new FantasyPlayer
                            {
                                ExternalPlayerId = squadPlayer.Id,
                                Name             = squadPlayer.Name,
                                Position         = pos,
                                TeamId           = dbTeam.Id,
                                Price            = DefaultPrice(pos),
                                IsActive         = true,
                                CreatedAt        = DateTime.UtcNow,
                                LastUpdatedAt    = DateTime.UtcNow,
                            };
                            _db.FantasyPlayers.Add(player);
                            existingPlayers[squadPlayer.Id] = player;
                            added++;
                        }
                    }

                    // Save after each team so progress isn't lost on error
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync squad for team {Name}", dbTeam.Name);
                }
            }

            _logger.LogInformation("Fantasy player sync complete: added={Added} updated={Updated} teams={Teams}",
                added, updated, callCount);
        }

        // ── Gameweek auto-creation from matchdays ─────────────────────

        /// <summary>
        /// Create a FantasyGameweek for each distinct MatchDay that doesn't already have one.
        /// Deadline = 1 hour before the first match in that matchday.
        /// </summary>
        public async Task SyncGameweeksFromMatchdaysAsync(CancellationToken ct = default)
        {
            var existingGameweeks = await _db.FantasyGameweeks.AsNoTracking()
                .Select(g => g.GameWeek)
                .ToListAsync(ct);

            var existingSet = new HashSet<int>(existingGameweeks);

            // Group upcoming matches by matchday
            var matchdays = await _db.Matches.AsNoTracking()
                .Where(m => m.MatchDay != null)
                .GroupBy(m => m.MatchDay!.Value)
                .Select(g => new
                {
                    MatchDay  = g.Key,
                    StartDate = g.Min(m => m.MatchDate),
                    EndDate   = g.Max(m => m.MatchDate),
                })
                .OrderBy(g => g.MatchDay)
                .ToListAsync(ct);

            int created = 0;
            foreach (var md in matchdays)
            {
                if (existingSet.Contains(md.MatchDay)) continue;

                _db.FantasyGameweeks.Add(new FantasyGameweek
                {
                    GameWeek      = md.MatchDay,
                    StartDate     = md.StartDate.Date,
                    EndDate       = md.EndDate.Date.AddDays(1),
                    Deadline      = md.StartDate.AddHours(-1),
                    IsLocked      = md.StartDate <= DateTime.UtcNow,
                    IsCompleted   = md.EndDate   <  DateTime.UtcNow.AddDays(-1),
                    CreatedAt     = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                });
                created++;
            }

            if (created > 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Auto-created {Count} fantasy gameweeks", created);
            }
        }

        // ── Match stats sync via api-sports ──────────────────────────

        public async Task SyncMatchStatsAsync(int internalMatchId, int externalMatchId, CancellationToken ct = default)
        {
            bool alreadyDone = await _db.PlayerMatchFantasyStats
                .AnyAsync(s => s.MatchId == internalMatchId, ct);
            if (alreadyDone) return;

            var match = await _db.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == internalMatchId, ct);
            if (match == null) return;

            // Find api-sports fixture by date across all configured leagues
            var date   = DateOnly.FromDateTime(match.MatchDate.ToUniversalTime());
            int season = date.Month >= 7 ? date.Year : date.Year - 1;

            int? fixtureId = null;
            var homeWord = match.HomeTeam.Name.Split(' ')[0];
            var awayWord = match.AwayTeam.Name.Split(' ')[0];

            foreach (var leagueId in ApiSportsPlayerSeedService.LeagueMap.Values)
            {
                var responses = await _apiSports.GetFixturesByDateAsync(date, leagueId, season, ct);
                var match_ = responses.FirstOrDefault(r =>
                    (r.Teams?.Home?.Name?.Contains(homeWord, StringComparison.OrdinalIgnoreCase) == true ||
                     r.Teams?.Away?.Name?.Contains(awayWord, StringComparison.OrdinalIgnoreCase) == true));

                if (match_ != null) { fixtureId = match_.Fixture.Id; break; }
            }

            if (fixtureId == null)
            {
                _logger.LogWarning("Could not find api-sports fixture for match {Id} on {Date}", internalMatchId, date);
                return;
            }

            var teamStats = await _apiSports.GetFixturePlayersAsync(fixtureId.Value, ct);
            if (teamStats.Count == 0)
            {
                _logger.LogWarning("No player stats from api-sports for fixture {Id}", fixtureId);
                return;
            }

            // Load all FantasyPlayers for name matching
            var allPlayers = await _db.FantasyPlayers.AsNoTracking().ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => NormaliseName(p.Name))
                .ToDictionary(g => g.Key, g => g.First());

            int recorded = 0;
            foreach (var team in teamStats)
            {
                foreach (var entry in team.Players)
                {
                    var stats = entry.Statistics.FirstOrDefault();
                    if (stats == null) continue;

                    int minutes = stats.Games.Minutes ?? 0;
                    bool played = minutes > 0;
                    int goals   = stats.Goals.Total ?? 0;
                    int assists = stats.Goals.Assists ?? 0;
                    int yellows = stats.Cards.Yellow ?? 0;
                    int reds    = stats.Cards.Red ?? 0;

                    if (!played && goals == 0 && assists == 0 && yellows == 0 && reds == 0) continue;

                    var player = FindPlayer(playerByName, entry.Player.Name);
                    if (player == null) continue;

                    int pts = FantasyServices.CalculatePlayerPoints(
                        player.Position, played, goals, assists, yellows, reds);

                    _db.PlayerMatchFantasyStats.Add(new PlayerMatchFantasyStat
                    {
                        FantasyPlayerId = player.Id,
                        MatchId         = internalMatchId,
                        IsHeAppeard     = played,
                        Goals           = goals,
                        Assists         = assists,
                        YellowCards     = yellows,
                        RedCard         = reds,
                        FantasyPoints   = pts,
                        CreatedAt       = DateTime.UtcNow,
                        LastUpdatedAt   = DateTime.UtcNow,
                    });
                    recorded++;
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Recorded api-sports stats for match {Id}: {Count} players", internalMatchId, recorded);
        }

        private static string NormaliseName(string name) =>
            name.ToLowerInvariant().Replace("-", " ").Replace(".", "").Trim();

        private static Models.FantasyModel.FantasyPlayer? FindPlayer(
            Dictionary<string, Models.FantasyModel.FantasyPlayer> dict, string apiName)
        {
            var norm = NormaliseName(apiName);
            if (dict.TryGetValue(norm, out var exact)) return exact;
            // Try last-name match
            var lastName = norm.Split(' ').Last();
            return dict.Values.FirstOrDefault(p =>
                NormaliseName(p.Name).EndsWith(lastName, StringComparison.OrdinalIgnoreCase));
        }

        // ── Full auto-score for a finished gameweek ───────────────────

        /// <summary>
        /// After all match stats for a gameweek are synced, aggregate into FantasyScore records.
        /// Called automatically from PredictionScoringJob.
        /// </summary>
        public async Task TryFinaliseGameweekScoresAsync(CancellationToken ct = default)
        {
            // Find gameweeks whose EndDate has passed and are not yet completed
            var now = DateTime.UtcNow;
            var due = await _db.FantasyGameweeks
                .Where(g => !g.IsCompleted && g.EndDate < now)
                .ToListAsync(ct);

            if (due.Count == 0) return;

            foreach (var gw in due)
            {
                try
                {
                    await _fantasyServices.CalculateGameweekScoresAsync(gw.Id, ct);
                    gw.IsCompleted    = true;
                    gw.IsLocked       = true;
                    gw.LastUpdatedAt  = now;
                    _logger.LogInformation("Finalised fantasy gameweek {GW}", gw.GameWeek);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to finalise gameweek {GW}", gw.GameWeek);
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}

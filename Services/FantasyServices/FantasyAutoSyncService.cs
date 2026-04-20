using BPFL.API.Data;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Services.External;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services.FantasyServices
{
    /// <summary>
    /// Automatically syncs fantasy players from squad data, creates gameweeks per matchday,
    /// and calculates player match stats from football-data.org match details.
    /// </summary>
    public class FantasyAutoSyncService
    {
        private readonly BPFL_DBContext _db;
        private readonly BPFLDataClient _dataClient;
        private readonly FantasyServices _fantasyServices;
        private readonly ILogger<FantasyAutoSyncService> _logger;

        public FantasyAutoSyncService(
            BPFL_DBContext db,
            BPFLDataClient dataClient,
            FantasyServices fantasyServices,
            ILogger<FantasyAutoSyncService> logger)
        {
            _db = db;
            _dataClient = dataClient;
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
        /// Fetch squad data for each league, create or update FantasyPlayers.
        /// Safe to call repeatedly — idempotent via ExternalPlayerId.
        /// </summary>
        public async Task SyncPlayersFromSquadsAsync(string[] leagueCodes, CancellationToken ct = default)
        {
            // Map ExternalTeamId → internal Team.Id
            var teamMap = await _db.Teams.AsNoTracking()
                .ToDictionaryAsync(t => t.ExternalId, t => t.Id, ct);

            var existingPlayers = await _db.FantasyPlayers
                .ToDictionaryAsync(p => p.ExternalPlayerId, p => p, ct);

            int added = 0, updated = 0;

            foreach (var code in leagueCodes)
            {
                try
                {
                    var response = await _dataClient.GetTeamAsync(code, ct);
                    var teams = response?.Teams ?? new();

                    foreach (var extTeam in teams)
                    {
                        if (!teamMap.TryGetValue(extTeam.Id, out var internalTeamId)) continue;

                        foreach (var squadPlayer in extTeam.Squad)
                        {
                            if (squadPlayer.Id <= 0) continue;

                            var pos = MapPosition(squadPlayer.Position);

                            if (existingPlayers.TryGetValue(squadPlayer.Id, out var existing))
                            {
                                existing.Name          = squadPlayer.Name;
                                existing.Position      = pos;
                                existing.TeamId        = internalTeamId;
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
                                    TeamId           = internalTeamId,
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
                    }

                    // Small delay to respect football-data.org rate limit (10 req/min)
                    await Task.Delay(700, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync players for league {Code}", code);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Fantasy player sync: added={Added} updated={Updated}", added, updated);
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

        // ── Match stats sync (goals / assists / bookings) ─────────────

        /// <summary>
        /// Fetch match detail from football-data.org and create/update PlayerMatchFantasyStats.
        /// Only players with recorded events (goal / assist / booking) are scored.
        /// Safe to call repeatedly — skips if stats already finalised for this match.
        /// </summary>
        public async Task SyncMatchStatsAsync(int internalMatchId, int externalMatchId, CancellationToken ct = default)
        {
            // Already processed?
            bool alreadyDone = await _db.PlayerMatchFantasyStats
                .AnyAsync(s => s.MatchId == internalMatchId, ct);
            if (alreadyDone)
            {
                _logger.LogDebug("Fantasy stats already recorded for match {Id}", internalMatchId);
                return;
            }

            var detail = await _dataClient.GetMatchDetailAsync(externalMatchId, ct);
            if (detail == null)
            {
                _logger.LogWarning("No match detail returned for externalId={Id}", externalMatchId);
                return;
            }

            // Build event map: externalPlayerId → (goals, assists, yellowCards, redCards)
            var events = new Dictionary<int, (int goals, int assists, int yellows, int reds)>();

            foreach (var goal in detail.Goals)
            {
                if (goal.Scorer?.Id > 0)
                {
                    var id = goal.Scorer.Id;
                    var ev = events.GetValueOrDefault(id);
                    events[id] = (ev.goals + 1, ev.assists, ev.yellows, ev.reds);
                }
                if (goal.Assist?.Id > 0)
                {
                    var id = goal.Assist.Id;
                    var ev = events.GetValueOrDefault(id);
                    events[id] = (ev.goals, ev.assists + 1, ev.yellows, ev.reds);
                }
            }

            foreach (var booking in detail.Bookings)
            {
                if (booking.Player?.Id > 0)
                {
                    var id = booking.Player.Id;
                    var ev = events.GetValueOrDefault(id);
                    bool isRed = booking.Card?.Contains("RED", StringComparison.OrdinalIgnoreCase) == true;
                    events[id] = isRed
                        ? (ev.goals, ev.assists, ev.yellows, ev.reds + 1)
                        : (ev.goals, ev.assists, ev.yellows + 1, ev.reds);
                }
            }

            if (events.Count == 0)
            {
                _logger.LogInformation("No events found in match {ExternalId} — skipping fantasy stats", externalMatchId);
                return;
            }

            // Map external player IDs to FantasyPlayer records
            var extIds = events.Keys.ToList();
            var players = await _db.FantasyPlayers.AsNoTracking()
                .Where(p => extIds.Contains(p.ExternalPlayerId))
                .ToDictionaryAsync(p => p.ExternalPlayerId, p => p, ct);

            int recorded = 0;
            foreach (var (extId, ev) in events)
            {
                if (!players.TryGetValue(extId, out var player)) continue;

                int pts = FantasyServices.CalculatePlayerPoints(
                    player.Position, appeared: true, ev.goals, ev.assists, ev.yellows, ev.reds);

                _db.PlayerMatchFantasyStats.Add(new PlayerMatchFantasyStat
                {
                    FantasyPlayerId = player.Id,
                    MatchId         = internalMatchId,
                    IsHeAppeard     = true,
                    Goals           = ev.goals,
                    Assists         = ev.assists,
                    YellowCards     = ev.yellows,
                    RedCard         = ev.reds,
                    FantasyPoints   = pts,
                    CreatedAt       = DateTime.UtcNow,
                    LastUpdatedAt   = DateTime.UtcNow,
                });
                recorded++;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Recorded fantasy stats for match {Id}: {Count} players", internalMatchId, recorded);
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

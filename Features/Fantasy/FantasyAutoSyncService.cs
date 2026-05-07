using BPFL.API.Data;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Shared.External;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Fantasy
{
    public class FantasyAutoSyncService
    {
        private readonly BPFL_DBContext _db;
        private readonly BPFLDataClient _dataClient;
        private readonly SportmonksClient _sportmonks;
        private readonly FantasyServices _fantasyServices;
        private readonly ILogger<FantasyAutoSyncService> _logger;

        public FantasyAutoSyncService(
            BPFL_DBContext db,
            BPFLDataClient dataClient,
            SportmonksClient sportmonks,
            FantasyServices fantasyServices,
            ILogger<FantasyAutoSyncService> logger)
        {
            _db = db;
            _dataClient = dataClient;
            _sportmonks = sportmonks;
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

        // ── Player sync from Sportmonks squads (BGL + any league with ExternalId) ──

        /// <summary>
        /// Fetch squads from Sportmonks for all DB teams that have a Sportmonks ExternalId.
        /// Safe to call repeatedly — upserts by ExternalPlayerId.
        /// </summary>
        public async Task SyncPlayersFromSportmonksAsync(CancellationToken ct = default)
        {
            var dbTeams = await _db.Teams.AsNoTracking()
                .Where(t => t.ExternalId > 0)
                .ToListAsync(ct);

            if (dbTeams.Count == 0)
            {
                _logger.LogWarning("No teams with Sportmonks ExternalId — skipping player sync.");
                return;
            }

            int added = 0, updated = 0;

            foreach (var team in dbTeams)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var squad = await _sportmonks.GetSquadByTeamIdAsync(team.ExternalId, ct);
                    _logger.LogInformation("Team {Name} (smId={Id}): {Count} squad entries returned",
                        team.Name, team.ExternalId, squad.Count);

                    if (squad.Count == 0)
                    {
                        _logger.LogWarning("Empty squad from Sportmonks for team {Name} (id={Id})", team.Name, team.ExternalId);
                        continue;
                    }

                    // Log a sample to diagnose position fields
                    var sample = squad.FirstOrDefault();
                    if (sample != null)
                        _logger.LogInformation(
                            "Sample player — PlayerId={Pid} Name={Name} PosId={PosId} PlayerPosId={PPosId} PosName={PosName}",
                            sample.PlayerId,
                            sample.Player?.CommonName ?? sample.Player?.Name,
                            sample.PositionId,
                            sample.Player?.PositionId,
                            sample.Player?.Position?.Name);

                    foreach (var sp in squad)
                    {
                        if (sp.PlayerId <= 0) continue;

                        var name = sp.Player?.CommonName
                                ?? sp.Player?.DisplayName
                                ?? sp.Player?.Name;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var pos = MapSportmonksPosition(sp);

                        var existing = await _db.FantasyPlayers
                            .FirstOrDefaultAsync(p => p.ExternalPlayerId == sp.PlayerId, ct);

                        var photoUrl = sp.Player?.ImagePath;

                        if (existing != null)
                        {
                            existing.Name          = name;
                            existing.Position      = pos;
                            existing.TeamId        = team.Id;
                            existing.IsActive      = true;
                            existing.LastUpdatedAt = DateTime.UtcNow;
                            if (!string.IsNullOrWhiteSpace(photoUrl))
                                existing.PhotoUrl  = photoUrl;
                            updated++;
                        }
                        else
                        {
                            _db.FantasyPlayers.Add(new FantasyPlayer
                            {
                                ExternalPlayerId = sp.PlayerId,
                                Name             = name,
                                Position         = pos,
                                TeamId           = team.Id,
                                Price            = DefaultPrice(pos),
                                IsActive         = true,
                                PhotoUrl         = photoUrl,
                                CreatedAt        = DateTime.UtcNow,
                                LastUpdatedAt    = DateTime.UtcNow,
                            });
                            added++;
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Sportmonks squad sync — {Team}: +{A} added, {U} updated", team.Name, added, updated);

                    await Task.Delay(300, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync Sportmonks squad for team {Name}", team.Name);
                }
            }

            _logger.LogInformation("Sportmonks player sync done: added={A} updated={U}", added, updated);
        }

        // Map Sportmonks squad entry → FantasyPosition
        // Priority: position name string → position_id (all known Sportmonks ID sets)
        private static FantasyPlayer.FantasyPosition MapSportmonksPosition(SmSquadPlayer sp)
        {
            // 1. Try position name/code from include=player.position
            var posName = sp.Player?.Position?.Name ?? sp.Player?.Position?.Code;
            if (!string.IsNullOrWhiteSpace(posName))
                return MapByPositionName(posName);

            // 2. Try position_id — cover all known Sportmonks ID sets
            var posId = sp.Player?.PositionId ?? sp.PositionId;
            return MapByPositionId(posId);
        }

        private static FantasyPlayer.FantasyPosition MapByPositionName(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("goalkeeper") || n.Contains("keeper") || n == "gk")
                return FantasyPlayer.FantasyPosition.GK;
            if (n.Contains("defender") || n.Contains("back") || n.Contains("centre-back") || n == "def")
                return FantasyPlayer.FantasyPosition.DEF;
            if (n.Contains("attacker") || n.Contains("forward") || n.Contains("striker") || n.Contains("winger") || n == "fwd")
                return FantasyPlayer.FantasyPosition.FWD;
            // midfield is default
            return FantasyPlayer.FantasyPosition.MID;
        }

        private static FantasyPlayer.FantasyPosition MapByPositionId(int? id) => id switch
        {
            // Sportmonks v3 general position IDs
            1  => FantasyPlayer.FantasyPosition.GK,
            2  => FantasyPlayer.FantasyPosition.DEF,
            3  => FantasyPlayer.FantasyPosition.MID,
            4  => FantasyPlayer.FantasyPosition.FWD,

            // Sportmonks v3 actual position IDs (confirmed from API)
            24 => FantasyPlayer.FantasyPosition.GK,   // Goalkeeper
            25 => FantasyPlayer.FantasyPosition.DEF,  // Defender
            26 => FantasyPlayer.FantasyPosition.MID,  // Midfielder
            27 => FantasyPlayer.FantasyPosition.FWD,  // Attacker ← key fix
            // Extended detailed IDs (just in case)
            28 => FantasyPlayer.FantasyPosition.MID,
            29 => FantasyPlayer.FantasyPosition.MID,
            30 => FantasyPlayer.FantasyPosition.MID,
            31 => FantasyPlayer.FantasyPosition.FWD,
            32 => FantasyPlayer.FantasyPosition.FWD,
            33 => FantasyPlayer.FantasyPosition.FWD,
            34 => FantasyPlayer.FantasyPosition.FWD,
            35 => FantasyPlayer.FantasyPosition.DEF,
            36 => FantasyPlayer.FantasyPosition.DEF,

            // Alternative Sportmonks ID set (type-based)
            148 => FantasyPlayer.FantasyPosition.GK,
            154 => FantasyPlayer.FantasyPosition.DEF,
            155 => FantasyPlayer.FantasyPosition.DEF,
            156 => FantasyPlayer.FantasyPosition.DEF,
            157 => FantasyPlayer.FantasyPosition.MID,
            158 => FantasyPlayer.FantasyPosition.MID,
            159 => FantasyPlayer.FantasyPosition.MID,
            160 => FantasyPlayer.FantasyPosition.FWD,
            161 => FantasyPlayer.FantasyPosition.FWD,
            162 => FantasyPlayer.FantasyPosition.FWD,
            163 => FantasyPlayer.FantasyPosition.FWD,

            _ => FantasyPlayer.FantasyPosition.MID
        };

        // ── Gameweek auto-creation from matchdays ─────────────────────

        /// <summary>
        /// Create a FantasyGameweek for each distinct MatchDay that doesn't already have one.
        /// Each gameweek opens Friday 12:00 UTC, deadline Friday 19:00 UTC,
        /// closes Tuesday 05:00 UTC — covering the full weekend + Monday fixtures.
        /// </summary>
        public async Task SyncGameweeksFromMatchdaysAsync(CancellationToken ct = default)
        {
            var existingGameweeks = await _db.FantasyGameweeks.AsNoTracking()
                .Select(g => g.GameWeek)
                .ToListAsync(ct);

            var existingSet = new HashSet<int>(existingGameweeks);

            var matchdays = await _db.Matches.AsNoTracking()
                .Where(m => m.MatchDay != null)
                .GroupBy(m => m.MatchDay!.Value)
                .Select(g => new
                {
                    MatchDay       = g.Key,
                    FirstMatchDate = g.Min(m => m.MatchDate),
                    LastMatchDate  = g.Max(m => m.MatchDate),
                })
                .OrderBy(g => g.MatchDay)
                .ToListAsync(ct);

            int created = 0;
            foreach (var md in matchdays)
            {
                if (existingSet.Contains(md.MatchDay)) continue;

                var (start, deadline, end) = GetGameweekWindow(md.FirstMatchDate);

                _db.FantasyGameweeks.Add(new FantasyGameweek
                {
                    GameWeek      = md.MatchDay,
                    StartDate     = start,
                    EndDate       = end,
                    Deadline      = deadline,
                    IsLocked      = deadline <= DateTime.UtcNow,
                    IsCompleted   = end      <  DateTime.UtcNow,
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

        /// <summary>
        /// Creates the next gameweek manually based on upcoming matches.
        /// Used by the Admin "Advance Gameweek" button.
        /// Returns the new gameweek number, or null if nothing to create.
        /// </summary>
        public async Task<int?> AdvanceGameweekAsync(CancellationToken ct = default)
        {
            var lastGw = await _db.FantasyGameweeks
                .OrderByDescending(g => g.GameWeek)
                .FirstOrDefaultAsync(ct);

            int nextNumber = (lastGw?.GameWeek ?? 0) + 1;

            // Find the next matchday after the last one we have a gameweek for
            var nextMatchday = await _db.Matches.AsNoTracking()
                .Where(m => m.MatchDay != null && m.MatchDay >= nextNumber)
                .GroupBy(m => m.MatchDay!.Value)
                .Select(g => new
                {
                    MatchDay       = g.Key,
                    FirstMatchDate = g.Min(m => m.MatchDate),
                    LastMatchDate  = g.Max(m => m.MatchDate),
                })
                .OrderBy(g => g.MatchDay)
                .FirstOrDefaultAsync(ct);

            if (nextMatchday == null) return null;

            var (start, deadline, end) = GetGameweekWindow(nextMatchday.FirstMatchDate);

            _db.FantasyGameweeks.Add(new FantasyGameweek
            {
                GameWeek      = nextMatchday.MatchDay,
                StartDate     = start,
                EndDate       = end,
                Deadline      = deadline,
                IsLocked      = false,
                IsCompleted   = false,
                CreatedAt     = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Advanced to fantasy gameweek {GW} ({Start} → {End})",
                nextMatchday.MatchDay, start, end);

            return nextMatchday.MatchDay;
        }

        /// <summary>
        /// Calculates the gameweek window based on the first match date.
        ///
        /// Cycle (one week):
        ///   Start    = Tuesday 10:00 UTC  — gameweek opens, team changes allowed
        ///   Deadline = Friday  10:00 UTC  — locked, no more changes
        ///   End      = Tuesday 10:00 UTC  — gameweek closes, scores calculated
        /// </summary>
        private static (DateTime start, DateTime deadline, DateTime end) GetGameweekWindow(DateTime firstMatch)
        {
            // Walk back to the nearest Friday (matches kick off on or after Friday)
            var friday = firstMatch.Date;
            while (friday.DayOfWeek != DayOfWeek.Friday)
                friday = friday.AddDays(-1);

            // Tuesday before that Friday = 3 days earlier
            var tuesday = friday.AddDays(-3);

            var start    = DateTime.SpecifyKind(tuesday.AddHours(10), DateTimeKind.Utc);        // Tue 10:00
            var deadline = DateTime.SpecifyKind(friday.AddHours(10), DateTimeKind.Utc);         // Fri 10:00
            var end      = DateTime.SpecifyKind(tuesday.AddDays(7).AddHours(10), DateTimeKind.Utc); // Tue+7 10:00

            return (start, deadline, end);
        }

        // ── Match stats sync via Sportmonks ──────────────────────────

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

            var date = DateOnly.FromDateTime(match.MatchDate.ToUniversalTime());
            var dayFixtures = await _sportmonks.GetFixturesByDateAsync(date, null, ct);

            // 1. Direct ExternalId match (BGL matches imported from Sportmonks)
            var smFixture = dayFixtures.FirstOrDefault(f => f.Id == match.ExternalId);

            // 2. Fuzzy match by team name
            if (smFixture == null)
            {
                var homeWord = match.HomeTeam.Name.Split(' ')[0];
                var awayWord = match.AwayTeam.Name.Split(' ')[0];
                smFixture = dayFixtures.FirstOrDefault(f =>
                {
                    var home = f.Participants.FirstOrDefault(p => p.Meta?.Location == "home");
                    var away = f.Participants.FirstOrDefault(p => p.Meta?.Location == "away");
                    return home?.Name?.Contains(homeWord, StringComparison.OrdinalIgnoreCase) == true
                        || away?.Name?.Contains(awayWord, StringComparison.OrdinalIgnoreCase) == true;
                });
            }

            if (smFixture == null)
            {
                _logger.LogWarning("No Sportmonks fixture found for match {Id} on {Date}", internalMatchId, date);
                return;
            }

            var smEvents = await _sportmonks.GetFixtureEventsAsync(smFixture.Id, ct);
            if (smEvents.Count == 0)
            {
                _logger.LogWarning("No events from Sportmonks for fixture {SmId}", smFixture.Id);
                return;
            }

            var allPlayers = await _db.FantasyPlayers.AsNoTracking().ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => NormaliseName(p.Name))
                .ToDictionary(g => g.Key, g => g.First());

            var playerStats = new Dictionary<int, (int goals, int assists, int yellows, int reds)>();
            int totalYellowCards = 0;

            foreach (var ev in smEvents)
            {
                if (ev.TypeId == SportmonksClient.EventType.Goal && ev.PlayerName != null)
                {
                    var scorer = FindPlayer(playerByName, ev.PlayerName);
                    if (scorer != null)
                    {
                        var cur = playerStats.GetValueOrDefault(scorer.Id);
                        playerStats[scorer.Id] = (cur.goals + 1, cur.assists, cur.yellows, cur.reds);
                    }
                    if (ev.RelatedPlayerName != null)
                    {
                        var assister = FindPlayer(playerByName, ev.RelatedPlayerName);
                        if (assister != null)
                        {
                            var cur = playerStats.GetValueOrDefault(assister.Id);
                            playerStats[assister.Id] = (cur.goals, cur.assists + 1, cur.yellows, cur.reds);
                        }
                    }
                }
                else if (ev.TypeId == SportmonksClient.EventType.YellowCard && ev.PlayerName != null)
                {
                    totalYellowCards++;
                    var player = FindPlayer(playerByName, ev.PlayerName);
                    if (player != null)
                    {
                        var cur = playerStats.GetValueOrDefault(player.Id);
                        playerStats[player.Id] = (cur.goals, cur.assists, cur.yellows + 1, cur.reds);
                    }
                }
                else if (ev.TypeId == SportmonksClient.EventType.RedCard && ev.PlayerName != null)
                {
                    var player = FindPlayer(playerByName, ev.PlayerName);
                    if (player != null)
                    {
                        var cur = playerStats.GetValueOrDefault(player.Id);
                        playerStats[player.Id] = (cur.goals, cur.assists, cur.yellows, cur.reds + 1);
                    }
                }
            }

            // ── Fetch corner statistics from Sportmonks ───────────────────
            int? totalCorners = null;
            try
            {
                var smStats = await _sportmonks.GetFixtureStatisticsAsync(smFixture.Id, ct);
                totalCorners = smStats
                    .Where(s => s.TypeId == SportmonksClient.StatCorners)
                    .Sum(s => s.Data?.Value ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch corner statistics for fixture {SmId}", smFixture.Id);
            }

            // Persist match-level stats for Corners / YellowCards bet resolution
            var matchToUpdate = await _db.Matches.FindAsync([internalMatchId], ct);
            if (matchToUpdate != null)
            {
                matchToUpdate.TotalCorners     = totalCorners;
                matchToUpdate.TotalYellowCards = totalYellowCards > 0 ? totalYellowCards : null;
            }

            int recorded = 0;
            foreach (var (playerId, stats) in playerStats)
            {
                var player = allPlayers.First(p => p.Id == playerId);
                int pts = FantasyServices.CalculatePlayerPoints(
                    player.Position, appeared: true, stats.goals, stats.assists, stats.yellows, stats.reds);

                _db.PlayerMatchFantasyStats.Add(new PlayerMatchFantasyStat
                {
                    FantasyPlayerId = playerId,
                    MatchId         = internalMatchId,
                    IsHeAppeard     = true,
                    Goals           = stats.goals,
                    Assists         = stats.assists,
                    YellowCards     = stats.yellows,
                    RedCard         = stats.reds,
                    FantasyPoints   = pts,
                    CreatedAt       = DateTime.UtcNow,
                    LastUpdatedAt   = DateTime.UtcNow,
                });
                recorded++;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Recorded Sportmonks stats for match {Id}: {Count} players, corners={Corners}, yellows={Yellows}",
                internalMatchId, recorded, totalCorners, totalYellowCards);
        }

        private static string NormaliseName(string name) =>
            name.ToLowerInvariant().Replace("-", " ").Replace(".", "").Trim();

        private static FantasyPlayer? FindPlayer(Dictionary<string, FantasyPlayer> dict, string apiName)
        {
            var norm = NormaliseName(apiName);
            if (dict.TryGetValue(norm, out var exact)) return exact;
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

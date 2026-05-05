using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Matches
{
    public class SportmonksMatchSyncService
    {
        private readonly BPFL_DBContext _db;
        private readonly SportmonksClient _sportmonks;
        private readonly ILogger<SportmonksMatchSyncService> _logger;

        // Sportmonks state IDs
        private const int StateFinished = 5;

        public SportmonksMatchSyncService(
            BPFL_DBContext db,
            SportmonksClient sportmonks,
            ILogger<SportmonksMatchSyncService> logger)
        {
            _db = db;
            _sportmonks = sportmonks;
            _logger = logger;
        }

        /// <summary>
        /// Sync matches for the given Sportmonks league code (BGL, PL, etc.)
        /// covering 7 days back and <daysAhead> days forward.
        /// </summary>
        public async Task<(int added, int updated)> SyncLeagueMatchesAsync(
            string leagueCode, int daysAhead = 30, CancellationToken ct = default)
        {
            if (!SportmonksClient.LeagueMap.TryGetValue(leagueCode.ToUpper(), out var leagueId))
                throw new ArgumentException($"Unknown Sportmonks league code: {leagueCode}. Valid: {string.Join(", ", SportmonksClient.LeagueMap.Keys)}");

            _currentLeagueCode = leagueCode.ToUpper();

            int added = 0, updated = 0;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            for (int i = -7; i <= daysAhead; i++)
            {
                if (ct.IsCancellationRequested) break;

                var date = today.AddDays(i);
                var fixtures = await _sportmonks.GetFixturesByDateAsync(date, [leagueId], ct);

                foreach (var fixture in fixtures)
                {
                    try
                    {
                        var (a, u) = await UpsertFixtureAsync(fixture, ct);
                        added += a; updated += u;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process fixture {Id}", fixture.Id);
                    }
                }

                if (fixtures.Count > 0)
                    await _db.SaveChangesAsync(ct);
            }

            _logger.LogInformation("Sportmonks {League} sync: added={Added} updated={Updated}", leagueCode, added, updated);
            return (added, updated);
        }

        /// <summary>
        /// Import historical finished matches for a league going back <daysBack> days.
        /// Uses the between/{start}/{end} endpoint — one request per page instead of one per day.
        /// </summary>
        public async Task<(int added, int updated)> SyncLeagueHistoryAsync(
            string leagueCode, int daysBack = 365, CancellationToken ct = default)
        {
            if (!SportmonksClient.LeagueMap.TryGetValue(leagueCode.ToUpper(), out var leagueId))
                throw new ArgumentException($"Unknown Sportmonks league code: {leagueCode}.");

            _currentLeagueCode = leagueCode.ToUpper();

            var to   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var from = to.AddDays(-daysBack);

            _logger.LogInformation("Fetching history for {League} from {From} to {To}", leagueCode, from, to);

            var fixtures = await _sportmonks.GetFixturesBetweenAsync(from, to, leagueId, ct);

            _logger.LogInformation("Got {Count} historical fixtures for {League}", fixtures.Count, leagueCode);

            int added = 0, updated = 0;

            foreach (var fixture in fixtures)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var (a, u) = await UpsertFixtureAsync(fixture, ct);
                    added += a; updated += u;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process historical fixture {Id}", fixture.Id);
                }
            }

            if (added + updated > 0)
                await _db.SaveChangesAsync(ct);

            _logger.LogInformation("History sync {League}: added={A} updated={U}", leagueCode, added, updated);
            return (added, updated);
        }

        // ── Shared upsert logic ───────────────────────────────────────

        private string _currentLeagueCode = "";

        private async Task<(int added, int updated)> UpsertFixtureAsync(SmFixture fixture, CancellationToken ct)
        {
            var home = fixture.Participants.FirstOrDefault(p => p.Meta?.Location == "home");
            var away = fixture.Participants.FirstOrDefault(p => p.Meta?.Location == "away");
            if (home == null || away == null) return (0, 0);

            var homeTeam = await EnsureTeamAsync(home, _currentLeagueCode, ct);
            var awayTeam = await EnsureTeamAsync(away, _currentLeagueCode, ct);

            if (!DateTime.TryParse(fixture.StartingAt, out var matchDate)) return (0, 0);
            matchDate = DateTime.SpecifyKind(matchDate, DateTimeKind.Utc);

            var status = fixture.StateId == StateFinished ? "FINISHED" : "TIMED";

            var homeScore = fixture.Scores
                .FirstOrDefault(s => s.Description == "CURRENT" && s.Score?.Participant == "home")
                ?.Score?.Goals;
            var awayScore = fixture.Scores
                .FirstOrDefault(s => s.Description == "CURRENT" && s.Score?.Participant == "away")
                ?.Score?.Goals;

            var existing = await _db.Matches
                .FirstOrDefaultAsync(m => m.ExternalId == fixture.Id, ct);

            if (existing == null)
            {
                var dayStart = matchDate.Date;
                var dayEnd   = dayStart.AddDays(1);
                existing = await _db.Matches.FirstOrDefaultAsync(m =>
                    m.HomeTeamId == homeTeam.Id &&
                    m.AwayTeamId == awayTeam.Id &&
                    m.MatchDate  >= dayStart &&
                    m.MatchDate  <  dayEnd, ct);

                if (existing != null)
                    existing.ExternalId = fixture.Id;
            }

            if (existing != null)
            {
                existing.HomeTeamId  = homeTeam.Id;
                existing.AwayTeamId  = awayTeam.Id;
                existing.MatchDate   = matchDate;
                existing.Status      = status;
                existing.LeagueCode  = _currentLeagueCode;
                if (homeScore.HasValue) existing.HomeScore = homeScore;
                if (awayScore.HasValue) existing.AwayScore = awayScore;
                return (0, 1);
            }

            _db.Matches.Add(new Match
            {
                ExternalId  = fixture.Id,
                HomeTeamId  = homeTeam.Id,
                AwayTeamId  = awayTeam.Id,
                MatchDate   = matchDate,
                Status      = status,
                LeagueCode  = _currentLeagueCode,
                HomeScore   = homeScore,
                AwayScore   = awayScore,
            });
            return (1, 0);
        }

        private async Task<Team> EnsureTeamAsync(SmParticipant participant, string leagueCode, CancellationToken ct)
        {
            // Match by Sportmonks ID stored in ExternalId
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.ExternalId == participant.Id, ct);
            if (team != null)
            {
                if (team.Name != participant.Name) team.Name = participant.Name;
                if (!string.IsNullOrEmpty(leagueCode)) team.LeagueCode = leagueCode;
                return team;
            }

            // Fuzzy name match — might be a team we already have under a different ID
            var all = await _db.Teams.ToListAsync(ct);
            team = all.FirstOrDefault(t =>
                string.Equals(t.Name, participant.Name, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Contains(participant.Name, StringComparison.OrdinalIgnoreCase) ||
                participant.Name.Contains(t.Name, StringComparison.OrdinalIgnoreCase));

            if (team != null)
            {
                team.ExternalId = participant.Id;
                team.Name = participant.Name;
                if (!string.IsNullOrEmpty(leagueCode)) team.LeagueCode = leagueCode;
                await _db.SaveChangesAsync(ct);
                return team;
            }

            // Create new team
            team = new Team { ExternalId = participant.Id, Name = participant.Name, LeagueCode = leagueCode };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);
            return team;
        }
    }
}

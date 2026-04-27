using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Services.External;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services.MatchServices
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
                        var home = fixture.Participants.FirstOrDefault(p => p.Meta?.Location == "home");
                        var away = fixture.Participants.FirstOrDefault(p => p.Meta?.Location == "away");
                        if (home == null || away == null) continue;

                        var homeTeam = await EnsureTeamAsync(home, ct);
                        var awayTeam = await EnsureTeamAsync(away, ct);

                        if (!DateTime.TryParse(fixture.StartingAt, out var matchDate)) continue;
                        matchDate = DateTime.SpecifyKind(matchDate, DateTimeKind.Utc);

                        var status = fixture.StateId == StateFinished ? "FINISHED" : "TIMED";

                        // Final score from CURRENT entries
                        var homeScore = fixture.Scores
                            .FirstOrDefault(s => s.Description == "CURRENT" && s.Score?.Participant == "home")
                            ?.Score?.Goals;
                        var awayScore = fixture.Scores
                            .FirstOrDefault(s => s.Description == "CURRENT" && s.Score?.Participant == "away")
                            ?.Score?.Goals;

                        var existing = await _db.Matches
                            .FirstOrDefaultAsync(m => m.ExternalId == fixture.Id, ct);

                        if (existing != null)
                        {
                            existing.HomeTeamId = homeTeam.Id;
                            existing.AwayTeamId = awayTeam.Id;
                            existing.MatchDate   = matchDate;
                            existing.Status      = status;
                            if (homeScore.HasValue) existing.HomeScore = homeScore;
                            if (awayScore.HasValue) existing.AwayScore = awayScore;
                            updated++;
                        }
                        else
                        {
                            _db.Matches.Add(new Match
                            {
                                ExternalId = fixture.Id,
                                HomeTeamId = homeTeam.Id,
                                AwayTeamId = awayTeam.Id,
                                MatchDate  = matchDate,
                                Status     = status,
                                HomeScore  = homeScore,
                                AwayScore  = awayScore,
                            });
                            added++;
                        }
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

        private async Task<Team> EnsureTeamAsync(SmParticipant participant, CancellationToken ct)
        {
            // Match by Sportmonks ID stored in ExternalId
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.ExternalId == participant.Id, ct);
            if (team != null)
            {
                if (team.Name != participant.Name) team.Name = participant.Name;
                return team;
            }

            // Fuzzy name match — might be a football-data.org team we already have
            var all = await _db.Teams.ToListAsync(ct);
            team = all.FirstOrDefault(t =>
                string.Equals(t.Name, participant.Name, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Contains(participant.Name, StringComparison.OrdinalIgnoreCase) ||
                participant.Name.Contains(t.Name, StringComparison.OrdinalIgnoreCase));

            if (team != null)
            {
                team.ExternalId = participant.Id;
                team.Name = participant.Name;
                await _db.SaveChangesAsync(ct);
                return team;
            }

            // Create new team
            team = new Team { ExternalId = participant.Id, Name = participant.Name };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);
            return team;
        }
    }
}

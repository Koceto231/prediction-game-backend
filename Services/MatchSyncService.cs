using BPFL.API.Data;
using BPFL.API.Models.DTO;
using BPFL.API.Models.ExternalDto;
using BPFL.API.Services.External;
using Microsoft.EntityFrameworkCore;
using Match = BPFL.API.Models.Match;

namespace BPFL.API.Services
{
    public class MatchSyncService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly BPFLDataClient dataClient;
        private readonly ILogger<MatchSyncService> _logger;

        public MatchSyncService(BPFL_DBContext _bPFL_DBContext, BPFLDataClient _dataClient, ILogger<MatchSyncService> logger)
        {
            bPFL_DBContext = _bPFL_DBContext;
            dataClient = _dataClient;
            _logger = logger;
        }

        public async Task<SyncResultDTO> ImportMatchesAsync(string leagueCode, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(leagueCode);
            leagueCode = leagueCode.Trim().ToUpper();

            var result = new SyncResultDTO();
            _logger.LogInformation("Starting match sync for league: {LeagueCode}", leagueCode);

            try
            {
                var response = await dataClient.GetAllMatchesAsync(leagueCode, ct);
                var externalMatches = response?.Matches ?? new List<ExternalMatchDTO>();

                if (externalMatches.Count == 0)
                {
                    _logger.LogWarning("No matches returned from API for league: {LeagueCode}", leagueCode);
                    return result;
                }

                var teamByExtId = await bPFL_DBContext.Teams.AsNoTracking().ToDictionaryAsync(t => t.ExternalId, t => t.Id, ct);

                var externalIds = externalMatches.Select(m => m.Id).ToList();

                var matchByExtId = await bPFL_DBContext.Matches.Where(m => externalIds.Contains(m.ExternalId)).ToDictionaryAsync(m => m.ExternalId, m => m, ct);

                bPFL_DBContext.ChangeTracker.AutoDetectChangesEnabled = false;

                foreach (var match in externalMatches)
                {
                    if (match.HomeTeam == null || match.AwayTeam == null)
                    {
                        _logger.LogWarning("Skipping match {MatchExternalId} because HomeTeam or AwayTeam is null", match.Id);
                        continue;
                    }

                    var homeTeamExternaIld = match.HomeTeam.Id;
                    var awayTeamExternalId = match.AwayTeam.Id;



                    if (!teamByExtId.TryGetValue(homeTeamExternaIld, out var homeId) || !teamByExtId.TryGetValue(awayTeamExternalId, out var awayId))
                    {
                        _logger.LogWarning(
                   "Skipping match {MatchExternalId} because one or both teams are missing. HomeExternalId: {HomeExternalId}, AwayExternalId: {AwayExternalId}",
                     match.Id,
                     homeTeamExternaIld,
                    awayTeamExternalId);
                        continue;
                    }

                    if (matchByExtId.TryGetValue(match.Id, out var existing))
                    {
                        existing.MatchDate = match.UtcDate;
                        existing.Status = match.Status;
                        existing.MatchDay = match.MatchDay;
                        existing.HomeTeamId = homeId;
                        existing.AwayTeamId = awayId;
                        existing.HomeScore = match.Score?.FullTime?.Home;
                        existing.AwayScore = match.Score?.FullTime?.Away;

                        _logger.LogDebug("Updated match with ExternalId: {ExternalId}", match.Id);
                        result.Updated++;
                    }
                    else
                    {
                        bPFL_DBContext.Matches.Add(new Match
                        {
                            ExternalId = match.Id,
                            MatchDate = match.UtcDate,
                            Status = match.Status,
                            MatchDay = match.MatchDay,
                            HomeTeamId = homeId,
                            AwayTeamId = awayId,
                            HomeScore = match.Score?.FullTime?.Home,
                            AwayScore = match.Score?.FullTime?.Away,

                        });
                        _logger.LogDebug("Added match with ExternalId: {ExternalId}", match.Id);
                        result.Added++;
                    }
                }

                bPFL_DBContext.ChangeTracker.AutoDetectChangesEnabled = true;
                await bPFL_DBContext.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Match sync completed for league: {LeagueCode}. Added: {Added}, Updated: {Updated}",
                    leagueCode,
                    result.Added,
                    result.Updated);
                return result;
            }catch(Exception ex)
            {
                bPFL_DBContext.ChangeTracker.AutoDetectChangesEnabled = true;

                _logger.LogError(ex, "Failed to sync matches for league: {LeagueCode}", leagueCode);
                throw;
                
            }
        }

    }
}

using BPFL.API.Data;
using BPFL.API.Models;

using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Matches
{
    public class TeamSyncService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly BPFLDataClient bPFL_DataClient;
        private readonly ILogger<TeamSyncService> _logger;


        public TeamSyncService(BPFL_DBContext _bPFL_DBContext, BPFLDataClient _bPFL_DataClient, ILogger<TeamSyncService> logger)
        {
            bPFL_DBContext = _bPFL_DBContext;
            bPFL_DataClient = _bPFL_DataClient;
            _logger = logger;
        }

        public async Task<SyncResultDTO> ImportTeamAsync(string competitionIdOrCode,CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(competitionIdOrCode);
            competitionIdOrCode = competitionIdOrCode.Trim().ToUpper();
            var result = new SyncResultDTO();
            _logger.LogInformation("Starting team sync for competition: {Competition}", competitionIdOrCode);
            try
            {
                var response = await bPFL_DataClient.GetTeamAsync(competitionIdOrCode, ct);

                var externalTeams = response?.Teams ?? new List<ExternalTeamDTO>();

                if (externalTeams.Count == 0)
                {
                    _logger.LogWarning("No teams returned from API for competition: {Competition}", competitionIdOrCode);
                    return result;
                }
                var externalIds = externalTeams
                                   .Select(t => t.Id)
                                   .ToList();

                var existingTeamsByExternalId = await bPFL_DBContext.Teams
                  .Where(t => externalIds.Contains(t.ExternalId))
                  .ToDictionaryAsync(t => t.ExternalId, t => t, ct);

                foreach (var externalTeam in externalTeams)
                {
                    try
                    {
                        if (!existingTeamsByExternalId.TryGetValue(externalTeam.Id, out var existing))
                        {
                            var team = new Team
                            {
                                ExternalId = externalTeam.Id,
                                Name = externalTeam.Name,

                            };
                            bPFL_DBContext.Teams.Add(team);
                            existingTeamsByExternalId[externalTeam.Id] = team;
                            result.Added++;

                            _logger.LogDebug(
                                "Added team with ExternalId: {ExternalId}, Name: {TeamName}",
                                externalTeam.Id,
                                externalTeam.Name);
                        }
                        else
                        {

                            existing.Name = externalTeam.Name;

                            result.Updated++;
                            _logger.LogDebug(
                               "Updated team with ExternalId: {ExternalId}, Name: {TeamName}",
                               externalTeam.Id,
                               externalTeam.Name);

                        }


                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error processing team {TeamId}: {TeamName}", externalTeam.Id, externalTeam.Name);

                    }
                }

                await bPFL_DBContext.SaveChangesAsync(ct);



            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during team sync for competition: {Competition}", competitionIdOrCode);
                throw;
            }

            _logger.LogInformation(
                "Team sync completed for competition: {Competition}. Added: {Added}, Updated: {Updated}",
                 competitionIdOrCode,
                 result.Added,
                 result.Updated);

            return result;



        }
    }
}

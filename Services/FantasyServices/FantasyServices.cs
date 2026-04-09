using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.FantasyDTO;
using BPFL.API.Models.FantasyModel;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace BPFL.API.Services.FantasyServices
{
    public class FantasyServices
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<FantasyServices> logger;

        public FantasyServices(BPFL_DBContext _bPFL_DBContext, ILogger<FantasyServices> _logger)
        {
            bPFL_DBContext = _bPFL_DBContext;
            logger = _logger;

        }

      public async Task<FantasyGameweekResponseDTO> GetCurrentFantasyGameweekAsync(CancellationToken ct = default)
        {
            var gameweek = await bPFL_DBContext.FantasyGameweeks
                .AsNoTracking().Where(o => o.IsCompleted == false).OrderBy(k => k.GameWeek)
                .FirstOrDefaultAsync(ct);

            if (gameweek == null)
            {
                throw new KeyNotFoundException("No active fantasy gameweek found.");
            }

            return new FantasyGameweekResponseDTO
            {
                Id = gameweek.Id,
                GameWeek = gameweek.GameWeek,
                StartDate = gameweek.StartDate,
                EndDate = gameweek.EndDate,
                Deadline = gameweek.Deadline,
                IsLocked = gameweek.IsLocked,
                IsCompleted = gameweek.IsCompleted
            };
        }

        public async Task CreateFantasyTeam(int userId, CreateFantasyTeamDTO createFantasyTeamDTO,CancellationToken ct = default)
        {
            if (userId <= 0)
            {
                throw new InvalidDataException();

            }

            if (string.IsNullOrWhiteSpace(createFantasyTeamDTO.TeamName))
            {
                throw new InvalidOperationException("Fantasy team name is required.");
            }

            var existingTeam = await bPFL_DBContext.FantasyTeams.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);

            if (existingTeam != null)
            {
                throw new InvalidOperationException("User already has a fantasy team.");
            }

            var fantasyTeam = new FantasyTeam
            {
                UserId = userId,
                TeamName = createFantasyTeamDTO.TeamName.Trim(),
                Budget = 100,
                RemainingBudget = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            bPFL_DBContext.FantasyTeams.Add(fantasyTeam);
            await bPFL_DBContext.SaveChangesAsync(ct);

        }

        public async Task<List<FantasyPlayerResponseDTO>> GetFantasyPlayersAsync(CancellationToken ct = default)
        {

            var players = await bPFL_DBContext.FantasyPlayers.AsNoTracking()
                .Where(k => k.IsActive == true).Select(k => new FantasyPlayerResponseDTO
                {
                   Id = k.Id,
                   Name = k.Name,
                   Position = k.Position.ToString(),
                   TeamId =  k.TeamId,
                   TeamName = k.Team.Name,
                   Price = k.Price,

                }).OrderBy(k => k.Position).ThenBy(k => k.Price).ToListAsync(ct);

            return players;

        }

        public async Task SaveFantasySelectionAsync(int userId ,SaveFantasySelectionDTO saveFantasySelectionDTO,CancellationToken ct = default)
        {
            if (userId <= 0)
            {
                throw new InvalidDataException();

            }

            var existingTeam = await bPFL_DBContext.FantasyTeams.FirstOrDefaultAsync(x => x.UserId == userId, ct);

            if (existingTeam == null)
            {
                throw new InvalidOperationException("User don't have a fantasy team.");
            }

            var existingGameWeek = await bPFL_DBContext.FantasyGameweeks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == saveFantasySelectionDTO.FantasyGameweekId, ct);

            if (existingGameWeek == null)
            {
                throw new InvalidOperationException();
            }

            if (existingGameWeek.IsLocked)
            {
                throw new InvalidOperationException();
            }


            if (saveFantasySelectionDTO.SelectedPlayerIds.Count != 11)
            {
                throw new InvalidOperationException();
            }

            if (saveFantasySelectionDTO.SelectedPlayerIds.Count != saveFantasySelectionDTO.SelectedPlayerIds.Distinct().Count())
            {
                throw new InvalidOperationException();
            }

            if (!saveFantasySelectionDTO.SelectedPlayerIds.Contains(saveFantasySelectionDTO.CaptainPlayerId))
            {
                throw new InvalidOperationException();
            }

            var players = await bPFL_DBContext.FantasyPlayers.AsNoTracking()
                .Where(k => saveFantasySelectionDTO.SelectedPlayerIds.Contains(k.Id))
                .ToListAsync(ct);

            var countGk = players.Count(k => k.Position == FantasyPlayer.FantasyPosition.GK);

            var countDef = players.Count(k => k.Position == FantasyPlayer.FantasyPosition.DEF);
            var countMid = players.Count(k => k.Position == FantasyPlayer.FantasyPosition.MID);
            var countFWD = players.Count(k => k.Position == FantasyPlayer.FantasyPosition.FWD);

            if (players.Count != 11 )
            {
                throw new InvalidOperationException();
            }

            if (countGk != 1 || countDef != 3 || countMid != 3 || countFWD != 4)
            {
                throw new InvalidOperationException();
            }

            if (players.GroupBy(x => x.TeamId).Any(g => g.Count() > 3))
            {
                throw new InvalidOperationException();
            }


            var oldFantasyTeamSelections = await bPFL_DBContext.FantasyTeamSelections
              .Where(k => k.FantasyTeamId == existingTeam.Id && k.FantasyGameweekId == saveFantasySelectionDTO.FantasyGameweekId).ToListAsync(ct);

            foreach (var selection in oldFantasyTeamSelections)
            {
                bPFL_DBContext.Remove(selection);
            }

            var fantasySelections = players.Select(k => new FantasyTeamSelection
            {
                FantasyTeamId = existingTeam.Id,
                FantasyGameweekId = saveFantasySelectionDTO.FantasyGameweekId,
                FantasyPlayerId = k.Id,
                IsCaptain = k.Id == saveFantasySelectionDTO.CaptainPlayerId,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            }).ToList();

            decimal totalPrice = players.Sum(x => x.Price);

            if (totalPrice > 100)
            {
                throw new InvalidOperationException();
            }

            existingTeam.RemainingBudget = existingTeam.Budget - totalPrice;


            bPFL_DBContext.AddRange(fantasySelections);

            await bPFL_DBContext.SaveChangesAsync(ct);
            

        }



        public async Task<FantasyTeamResponseDTO> GetMyFantasyTeamAsync(int userId, int fantasyGameweekId,
            CancellationToken ct = default)
        {
            if (userId <= 0)
            {
                throw new InvalidOperationException();
            }

            var fantasyTeam = await bPFL_DBContext.FantasyTeams.AsNoTracking().Where(k => k.UserId == userId).FirstOrDefaultAsync(ct);

            if (fantasyTeam == null)
            {

                throw new InvalidOperationException();
            }

            var gameweek = await bPFL_DBContext.FantasyGameweeks.AsNoTracking().Where(k => k.Id == fantasyGameweekId).FirstOrDefaultAsync(ct);

            if (gameweek == null)
            {

                throw new InvalidOperationException();
            }

            var selections = await bPFL_DBContext.FantasyTeamSelections.AsNoTracking()
                .Where(k => k.FantasyGameweekId == gameweek.Id && k.FantasyTeamId == fantasyTeam.Id)
                .Select(k => new FantasySelectedPlayerResponseDTO
                {
                    FantasyPlayerId = k.FantasyPlayerId,
                    Name = k.FantasyPlayer.Name,
                    TeamName = k.FantasyPlayer.Team.Name,
                    Position = k.FantasyPlayer.Position.ToString(),
                    Price = k.FantasyPlayer.Price,
                    IsCaptain = k.IsCaptain,
                    Points = 0
                })
                .ToListAsync(ct);

            var fantasyScore = await bPFL_DBContext.FantasyScores.Where(k => k.FantasyTeamId == fantasyTeam.Id && k.FantasyGameweekId == gameweek.Id).FirstOrDefaultAsync(ct);



            var fantasyResponse = new FantasyTeamResponseDTO
            {
                FantasyTeamId = fantasyTeam.Id,
                TeamName = fantasyTeam.TeamName,
                Budget = fantasyTeam.Budget,
                RemainingBudget = fantasyTeam.RemainingBudget,
                FantasyGameweekId = gameweek.Id,
                GameWeek = gameweek.GameWeek,
                IsLocked = gameweek.IsLocked,
                WeeklyPoints = fantasyScore != null ? fantasyScore.WeeklyPoints : 0,
                Players = selections
            };

            return fantasyResponse;
        }


        public async Task<List<FantasyLeaderboardRowDTO>> GetFantasyLeaderboardAsync(int fantasyGameweekId, CancellationToken ct = default)
        {
            if (fantasyGameweekId <= 0)
            {
                throw new InvalidOperationException();
            }

            var gameweek = await bPFL_DBContext.FantasyGameweeks.AsNoTracking()
                .Where(x => x.Id == fantasyGameweekId).FirstOrDefaultAsync(ct);

            if (gameweek == null)
            {
                throw new InvalidOperationException();
            }

            var fantasyScores = await bPFL_DBContext.FantasyScores.AsNoTracking().Where(x => x.FantasyGameweekId == fantasyGameweekId)
                .Select(x => new { x.FantasyTeam.UserId, x.FantasyTeam.User.Username, x.FantasyTeam.TeamName, x.WeeklyPoints })
                .OrderByDescending(x => x.WeeklyPoints).ThenBy(x => x.TeamName)
                .ToListAsync(ct);

            var leaderboard = new List<FantasyLeaderboardRowDTO>
            {

            };
            for (int i = 0; i < fantasyScores.Count; i++)
            {
                var row = new FantasyLeaderboardRowDTO
                {
                    Rank = i + 1,
                    UserId = fantasyScores[i].UserId,
                    Username = fantasyScores[i].Username,
                    FantasyTeamName = fantasyScores[i].TeamName,
                    WeeklyPoints = fantasyScores[i].WeeklyPoints,
                };

                leaderboard.Add(row);

            }

            return leaderboard;

        }
        }


    }


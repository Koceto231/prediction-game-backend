using BPFL.API.Data;
using BPFL.API.Exceptions;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public enum LeagueErrorType
    {
        NotFound,
        InvalidInviteCode,
        AlreadyMember,
        NotMember,
        NotOwner,
        NameRequired,
        CannotLeaveAsOwner
    }


    public class LeagueService
    {
        private readonly BPFL_DBContext _bPFL_DBContext;
        private readonly ILogger<LeagueService> _logger;

        public LeagueService(BPFL_DBContext bPFL_DBContext, ILogger<LeagueService> logger)
        {
            _bPFL_DBContext = bPFL_DBContext;
            _logger = logger;
        }

        public async Task<LeagueResponseDTO> CreateLeagueAsync(int userId, CreateLeagueDTO createLeagueDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createLeagueDTO);
            ValidateUserId(userId);

            if (string.IsNullOrWhiteSpace(createLeagueDTO.Name))
            {
                throw new LeagueException("League name is required", LeagueErrorType.NameRequired);
            }

            var inveteToken = GenerateInviteCode();

            while(await _bPFL_DBContext.Leagues.AnyAsync(k => k.InviteCode == inveteToken,ct))
                inveteToken = GenerateInviteCode();

            var league = new League
            {
                Name = createLeagueDTO.Name,
                InviteCode = inveteToken,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                Members = new List<LeagueMember>
    {
        new LeagueMember
        {
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        }
    }
            };

            _bPFL_DBContext.Leagues.Add(league);
            await _bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation(
              "League {LeagueId} '{Name}' created by user {UserId} with code {Code}",
              league.Id, league.Name, userId, inveteToken);

            return await GetLeagueResponseAsync(league.Id,ct);
        }

        public async Task<LeagueResponseDTO> JoinLeagueAsync(int userId, JoinLeagueDTO joinLeagueDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(joinLeagueDTO);
            ValidateUserId(userId);

            if (string.IsNullOrWhiteSpace(joinLeagueDTO.InviteCode))
            {
                throw new LeagueException("Invite code is required.", LeagueErrorType.InvalidInviteCode);
            }

            var league = await _bPFL_DBContext.Leagues.FirstOrDefaultAsync(l => l.InviteCode == joinLeagueDTO.InviteCode, ct);

            if (league == null)
            {
                throw new LeagueException(
                   $"No league found with invite code '{joinLeagueDTO.InviteCode}'.",
                   LeagueErrorType.InvalidInviteCode);
            }

            var alreadyMember = await _bPFL_DBContext.LeagueMembers.AnyAsync(s => s.UserId == userId && s.LeagueId == league.Id, ct);

            if (alreadyMember)
                throw new LeagueException("You are already a member of this league.", LeagueErrorType.AlreadyMember);

            _bPFL_DBContext.LeagueMembers.Add(new LeagueMember
            {
                UserId = userId,
                LeagueId = league.Id,
                JoinedAt = DateTime.UtcNow,
            });

            await _bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} joined league {LeagueId}", userId, league.Id);

            return await GetLeagueResponseAsync(league.Id, ct);
        }

        public async Task LeaveLeagueAsync(int userId, int leagueId, CancellationToken ct = default)
        {
            ValidateUserId(userId);

            var league = await _bPFL_DBContext.Leagues.FindAsync(leagueId,ct) ??
                throw new LeagueException($"League {leagueId} not found.", LeagueErrorType.NotFound);

            if (league.OwnerId == userId)
            {
                throw new LeagueException(
                   "The owner cannot leave the league. Delete it instead.",
                   LeagueErrorType.CannotLeaveAsOwner);
            }

            var membership = await _bPFL_DBContext.LeagueMembers.FirstOrDefaultAsync(member => league.Id == leagueId && member.UserId == userId,ct)
                ?? throw new LeagueException("You are not a member of this league.", LeagueErrorType.NotMember);

            _bPFL_DBContext.LeagueMembers.Remove(membership);
            await _bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} left league {LeagueId}", userId, leagueId);

        }

        public async Task DeleteLeague(int userId, int leagueId, CancellationToken ct = default)
        {
            ValidateUserId(userId);
            var league = await _bPFL_DBContext.Leagues.Include(m => m.Members)
                .FirstOrDefaultAsync(l => l.Id == leagueId,ct)
            ?? throw new LeagueException($"League {leagueId} not found.", LeagueErrorType.NotFound);

            if(league.OwnerId != userId)
            {
                throw new LeagueException("Only the owner can delete the league.", LeagueErrorType.NotOwner);
            }

            _bPFL_DBContext.LeagueMembers.RemoveRange(league.Members);
            _bPFL_DBContext.Leagues.Remove(league);

            await _bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation("League {LeagueId} deleted by owner {UserId}", leagueId, userId);

        }

        public async Task<List<LeagueResponseDTO>> GetMyLeages(int userId, CancellationToken ct = default)
        {
            ValidateUserId(userId);

            return await _bPFL_DBContext.Leagues
                 .AsNoTracking()
                 .Where(l => l.Members.Any(m => m.UserId == userId))
                 .Select(l => new LeagueResponseDTO
                 {
                     Id = l.Id,
                     Name = l.Name,
                     InviteCode = l.InviteCode,
                     OwnerUsername = l.Owner.Username,
                     MemberCount = l.Members.Count,
                     CreatedAt = l.CreatedAt
                 })
                 .ToListAsync(ct);
        }

        public async Task<List<LeagueLeaderboardEntryDTO>> GetLeagueLeaderboardsAsync(int userId, int leagueId,
             CancellationToken ct = default)
        {
            ValidateUserId(userId);

            var league = await _bPFL_DBContext.Leagues
                .AsNoTracking()
                .Where(k => k.Id == leagueId)
                .Select(l => new { l.Id, IsMember = l.Members.Any(m => m.UserId == userId) })
                .FirstOrDefaultAsync(ct);

            if (league == null)
                throw new LeagueException($"League {leagueId} not found.", LeagueErrorType.NotFound);

            if (!league.IsMember)
            {
                throw new LeagueException(
                   "You must be a member to view this league's leaderboard.",
                   LeagueErrorType.NotMember);
            }

            var membersId = await _bPFL_DBContext.LeagueMembers.AsNoTracking().Where(m => m.LeagueId == leagueId)
                .Select(k => k.UserId).ToListAsync(ct); 

            var entries = await _bPFL_DBContext.Predictions.AsNoTracking().Where(p => membersId.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new LeagueLeaderboardEntryDTO
                {
                    UserId = g.Key,
                    Username = g.First().User.Username,
                    TotalPoints = g.Sum(p => p.Points),
                    CorrectResults = g.Count(p => p.Points == 3),
                    TotalPredictions = g.Count()
                })
                .ToListAsync(ct);

            var userWithPrediction = entries.Select(k => k.UserId).ToHashSet();

            var usersWithoutPredictions = await _bPFL_DBContext.Users.AsNoTracking()

                .Where(u => membersId.Contains(u.Id) && !userWithPrediction.Contains(u.Id))
                .Select(g => new LeagueLeaderboardEntryDTO
                {
                    UserId = g.Id,
                    Username = g.Username,
                    TotalPoints = 0,
                    CorrectResults = 0,
                    TotalPredictions = 0
                })
                .ToListAsync(ct);

            entries.AddRange(usersWithoutPredictions);

            entries = entries
                .OrderByDescending(e => e.TotalPoints)
        .ThenByDescending(e => e.CorrectResults)
        .ThenBy(e => e.Username)
        .ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Rank = i + 1;
            }

            return entries;
        }

        private static void ValidateUserId(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("Invalid user ID.", nameof(userId));
        }

        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(Enumerable.Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        }

        private async Task<LeagueResponseDTO> GetLeagueResponseAsync(int leagueId, CancellationToken ct)
        {
            return await _bPFL_DBContext.Leagues
                .AsNoTracking()
                .Where(l => l.Id == leagueId)
                .Select(l => new LeagueResponseDTO
                {
                    Id = l.Id,
                    Name = l.Name,
                    InviteCode = l.InviteCode,
                    OwnerUsername = l.Owner.Username,
                    MemberCount = l.Members.Count,
                    CreatedAt = l.CreatedAt
                })
                .FirstAsync(ct);
        }
    }


}

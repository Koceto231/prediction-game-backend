using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Features.Fantasy;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Shared;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Fantasy
{
    public class FantasyServices
    {
        private readonly BPFL_DBContext _db;
        private readonly ILogger<FantasyServices> _logger;
        private readonly IAppCache _cache;

        private const string CurrentGameweekKey = "fantasy:gameweek:current";
        private const string PlayersKey          = "fantasy:players";
        private static string LeaderboardKey(int gwId) => $"fantasy:leaderboard:{gwId}";

        private static readonly TimeSpan GameweekTtl    = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PlayersTtl     = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LeaderboardTtl = TimeSpan.FromMinutes(2);

        public FantasyServices(BPFL_DBContext db, ILogger<FantasyServices> logger, IAppCache cache)
        {
            _db     = db;
            _logger = logger;
            _cache  = cache;
        }

        // ── Scoring rules ─────────────────────────────────────────────
        public static int CalculatePlayerPoints(
            FantasyPlayer.FantasyPosition position,
            bool appeared, int goals, int assists, int yellowCards, int redCards)
        {
            if (!appeared) return 0;

            int pts = 2; // appearance

            pts += position switch
            {
                FantasyPlayer.FantasyPosition.GK  => goals * 10,
                FantasyPlayer.FantasyPosition.DEF => goals * 6,
                FantasyPlayer.FantasyPosition.MID => goals * 5,
                FantasyPlayer.FantasyPosition.FWD => goals * 4,
                _ => 0
            };

            pts += assists * 3;
            pts -= yellowCards;
            pts -= redCards * 3;

            return Math.Max(pts, 0);
        }

        // ── Gameweek ──────────────────────────────────────────────────

        public async Task<FantasyGameweekResponseDTO?> GetCurrentFantasyGameweekAsync(CancellationToken ct = default)
        {
            var cached = await _cache.GetAsync<FantasyGameweekResponseDTO>(CurrentGameweekKey, ct);
            if (cached != null) return cached;

            var gameweek = await _db.FantasyGameweeks
                .AsNoTracking()
                .Where(g => !g.IsCompleted)
                .OrderBy(g => g.GameWeek)
                .FirstOrDefaultAsync(ct);

            if (gameweek == null) return null;

            var result = MapGameweek(gameweek);
            await _cache.SetAsync(CurrentGameweekKey, result, GameweekTtl, ct);
            return result;
        }

        public async Task<FantasyGameweekResponseDTO> CreateGameweekAsync(CreateFantasyGameweekDTO dto, CancellationToken ct = default)
        {
            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("EndDate must be after StartDate.");

            var gw = new FantasyGameweek
            {
                GameWeek   = dto.GameWeek,
                StartDate  = dto.StartDate,
                EndDate    = dto.EndDate,
                Deadline   = dto.Deadline,
                IsLocked   = false,
                IsCompleted = false,
                CreatedAt  = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            _db.FantasyGameweeks.Add(gw);
            await _db.SaveChangesAsync(ct);
            return MapGameweek(gw);
        }

        // ── Players ───────────────────────────────────────────────────

        public async Task<List<FantasyPlayerResponseDTO>> GetFantasyPlayersAsync(CancellationToken ct = default)
        {
            var cached = await _cache.GetAsync<List<FantasyPlayerResponseDTO>>(PlayersKey, ct);
            if (cached != null) return cached;

            // Fetch from DB first, then map in memory to avoid EF/Npgsql
            // failing to translate enum.ToString() to SQL
            var players = await _db.FantasyPlayers
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Include(p => p.Team)
                .OrderBy(p => p.Position)
                .ThenBy(p => p.Price)
                .ToListAsync(ct);

            var result = players.Select(p => new FantasyPlayerResponseDTO
            {
                Id         = p.Id,
                Name       = p.Name,
                Position   = p.Position.ToString(),
                TeamId     = p.TeamId,
                TeamName   = p.Team?.Name ?? "",
                Price      = p.Price,
                PhotoUrl   = p.PhotoUrl,
                LeagueCode = p.Team?.LeagueCode,
            }).ToList();

            await _cache.SetAsync(PlayersKey, result, PlayersTtl, ct);
            return result;
        }

        public async Task<FantasyPlayerResponseDTO> AddPlayerAsync(AddFantasyPlayerDTO dto, CancellationToken ct = default)
        {
            if (!Enum.TryParse<FantasyPlayer.FantasyPosition>(dto.Position, true, out var pos))
                throw new ArgumentException($"Invalid position '{dto.Position}'. Use GK, DEF, MID or FWD.");

            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == dto.TeamId, ct)
                ?? throw new KeyNotFoundException($"Team {dto.TeamId} not found.");

            var player = new FantasyPlayer
            {
                Name             = dto.Name.Trim(),
                Position         = pos,
                TeamId           = dto.TeamId,
                Price            = dto.Price,
                ExternalPlayerId = dto.ExternalPlayerId,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow,
                LastUpdatedAt    = DateTime.UtcNow
            };

            _db.FantasyPlayers.Add(player);
            await _db.SaveChangesAsync(ct);
            await _cache.RemoveAsync(PlayersKey, ct);

            return new FantasyPlayerResponseDTO
            {
                Id       = player.Id,
                Name     = player.Name,
                Position = player.Position.ToString(),
                TeamId   = player.TeamId,
                TeamName = team.Name,
                Price    = player.Price
            };
        }

        // ── Team management ───────────────────────────────────────────

        public async Task CreateFantasyTeam(int userId, CreateFantasyTeamDTO dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.TeamName))
                throw new InvalidOperationException("Fantasy team name is required.");

            var existing = await _db.FantasyTeams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId, ct);

            if (existing != null)
                throw new InvalidOperationException("User already has a fantasy team.");

            _db.FantasyTeams.Add(new FantasyTeam
            {
                UserId          = userId,
                TeamName        = dto.TeamName.Trim(),
                Budget          = 100,
                RemainingBudget = 100,
                CreatedAt       = DateTime.UtcNow,
                UpdatedAt       = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(ct);
        }

        // ── Selection ─────────────────────────────────────────────────

        public async Task SaveFantasySelectionAsync(int userId, SaveFantasySelectionDTO dto, CancellationToken ct = default)
        {
            // Auto-create a FantasyTeam for the user if one doesn't exist yet
            var team = await _db.FantasyTeams.FirstOrDefaultAsync(t => t.UserId == userId, ct);
            if (team == null)
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
                team = new FantasyTeam
                {
                    UserId          = userId,
                    TeamName        = user != null ? $"{user.Username}'s Team" : "My Fantasy Team",
                    Budget          = 100,
                    RemainingBudget = 100,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow,
                };
                _db.FantasyTeams.Add(team);
                await _db.SaveChangesAsync(ct);
            }

            var gameweek = await _db.FantasyGameweeks.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == dto.FantasyGameweekId, ct)
                ?? throw new KeyNotFoundException("Gameweek not found.");

            // Allow editing if the gameweek has not completed yet (IsCompleted = hard lock)
            if (gameweek.IsCompleted)
                throw new InvalidOperationException("Gameweek is completed — selections are closed.");

            if (dto.SelectedPlayerIds.Count != 11)
                throw new InvalidOperationException("You must select exactly 11 players.");

            if (dto.SelectedPlayerIds.Distinct().Count() != 11)
                throw new InvalidOperationException("Duplicate players in selection.");

            if (!dto.SelectedPlayerIds.Contains(dto.CaptainPlayerId))
                throw new InvalidOperationException("Captain must be one of the selected players.");

            var players = await _db.FantasyPlayers.AsNoTracking()
                .Where(p => dto.SelectedPlayerIds.Contains(p.Id))
                .ToListAsync(ct);

            if (players.Count != 11)
                throw new InvalidOperationException("One or more selected players do not exist.");

            var gk  = players.Count(p => p.Position == FantasyPlayer.FantasyPosition.GK);
            var def = players.Count(p => p.Position == FantasyPlayer.FantasyPosition.DEF);
            var mid = players.Count(p => p.Position == FantasyPlayer.FantasyPosition.MID);
            var fwd = players.Count(p => p.Position == FantasyPlayer.FantasyPosition.FWD);

            if (gk != 1 || def != 3 || mid != 3 || fwd != 4)
                throw new InvalidOperationException("Invalid formation. Required: 1 GK, 3 DEF, 3 MID, 4 FWD.");

            if (players.GroupBy(p => p.TeamId).Any(g => g.Count() > 3))
                throw new InvalidOperationException("Maximum 3 players from the same club.");

            decimal totalPrice = players.Sum(p => p.Price);
            if (totalPrice > 100)
                throw new InvalidOperationException($"Total price {totalPrice} exceeds budget of 100.");

            // Replace old selections
            var old = await _db.FantasyTeamSelections
                .Where(s => s.FantasyTeamId == team.Id && s.FantasyGameweekId == dto.FantasyGameweekId)
                .ToListAsync(ct);
            _db.FantasyTeamSelections.RemoveRange(old);

            var selections = players.Select(p => new FantasyTeamSelection
            {
                FantasyTeamId     = team.Id,
                FantasyGameweekId = dto.FantasyGameweekId,
                FantasyPlayerId   = p.Id,
                IsCaptain         = p.Id == dto.CaptainPlayerId,
                CreatedAt         = DateTime.UtcNow,
                LastUpdatedAt     = DateTime.UtcNow,
            }).ToList();

            _db.FantasyTeamSelections.AddRange(selections);
            team.RemainingBudget = team.Budget - totalPrice;
            await _db.SaveChangesAsync(ct);
        }

        // ── Team view ─────────────────────────────────────────────────

        public async Task<FantasyTeamResponseDTO?> GetMyFantasyTeamAsync(
            int userId, int fantasyGameweekId, CancellationToken ct = default)
        {
            var team = await _db.FantasyTeams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId, ct);

            if (team == null) return null;

            var gameweek = await _db.FantasyGameweeks.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == fantasyGameweekId, ct)
                ?? throw new KeyNotFoundException("Gameweek not found.");

            var selections = await _db.FantasyTeamSelections.AsNoTracking()
                .Where(s => s.FantasyGameweekId == gameweek.Id && s.FantasyTeamId == team.Id)
                .Include(s => s.FantasyPlayer).ThenInclude(p => p.Team)
                .ToListAsync(ct);

            // Fetch player points earned this gameweek (sum across all matches in the window)
            var playerIds = selections.Select(s => s.FantasyPlayerId).ToList();
            var stats = await _db.PlayerMatchFantasyStats.AsNoTracking()
                .Where(st => playerIds.Contains(st.FantasyPlayerId)
                          && st.Match.MatchDate >= gameweek.StartDate
                          && st.Match.MatchDate <= gameweek.EndDate)
                .GroupBy(st => st.FantasyPlayerId)
                .Select(g => new { PlayerId = g.Key, Points = g.Sum(st => st.FantasyPoints) })
                .ToListAsync(ct);

            var pointsMap = stats.ToDictionary(s => s.PlayerId, s => s.Points);

            var players = selections.Select(s =>
            {
                int basePoints = pointsMap.GetValueOrDefault(s.FantasyPlayerId, 0);
                int totalPoints = s.IsCaptain ? basePoints * 2 : basePoints;
                return new FantasySelectedPlayerResponseDTO
                {
                    FantasyPlayerId = s.FantasyPlayerId,
                    Name            = s.FantasyPlayer.Name,
                    Position        = s.FantasyPlayer.Position.ToString(),
                    TeamName        = s.FantasyPlayer.Team.Name,
                    Price           = s.FantasyPlayer.Price,
                    IsCaptain       = s.IsCaptain,
                    Points          = totalPoints,
                };
            }).ToList();

            var score = await _db.FantasyScores.AsNoTracking()
                .FirstOrDefaultAsync(sc => sc.FantasyTeamId == team.Id && sc.FantasyGameweekId == gameweek.Id, ct);

            return new FantasyTeamResponseDTO
            {
                FantasyTeamId   = team.Id,
                TeamName        = team.TeamName,
                Budget          = team.Budget,
                RemainingBudget = team.RemainingBudget,
                FantasyGameweekId = gameweek.Id,
                GameWeek        = gameweek.GameWeek,
                IsLocked        = gameweek.IsLocked,
                WeeklyPoints    = score?.WeeklyPoints ?? players.Sum(p => p.Points),
                Players         = players,
            };
        }

        public async Task<FantasyTeamResponseDTO?> GetMyTeamForCurrentGameweekAsync(int userId, CancellationToken ct = default)
        {
            var gameweek = await _db.FantasyGameweeks.AsNoTracking()
                .Where(g => !g.IsCompleted)
                .OrderBy(g => g.GameWeek)
                .FirstOrDefaultAsync(ct);

            if (gameweek == null) return null;

            return await GetMyFantasyTeamAsync(userId, gameweek.Id, ct);
        }

        // ── Leaderboard ───────────────────────────────────────────────

        public async Task<List<FantasyLeaderboardRowDTO>> GetFantasyLeaderboardAsync(
            int fantasyGameweekId, CancellationToken ct = default)
        {
            var cacheKey = LeaderboardKey(fantasyGameweekId);
            var cached   = await _cache.GetAsync<List<FantasyLeaderboardRowDTO>>(cacheKey, ct);
            if (cached != null) return cached;

            _ = await _db.FantasyGameweeks.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == fantasyGameweekId, ct)
                ?? throw new KeyNotFoundException("Gameweek not found.");

            var rows = await _db.FantasyScores.AsNoTracking()
                .Where(sc => sc.FantasyGameweekId == fantasyGameweekId)
                .Select(sc => new
                {
                    sc.FantasyTeam.UserId,
                    sc.FantasyTeam.User.Username,
                    sc.FantasyTeam.TeamName,
                    sc.WeeklyPoints
                })
                .OrderByDescending(r => r.WeeklyPoints)
                .ThenBy(r => r.TeamName)
                .ToListAsync(ct);

            var result = rows.Select((r, i) => new FantasyLeaderboardRowDTO
            {
                Rank            = i + 1,
                UserId          = r.UserId,
                Username        = r.Username,
                FantasyTeamName = r.TeamName,
                WeeklyPoints    = r.WeeklyPoints,
            }).ToList();

            await _cache.SetAsync(cacheKey, result, LeaderboardTtl, ct);
            return result;
        }

        // ── Admin: player stats ───────────────────────────────────────

        public async Task SubmitPlayerStatsAsync(SubmitPlayerStatsDTO dto, CancellationToken ct = default)
        {
            var player = await _db.FantasyPlayers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.FantasyPlayerId, ct)
                ?? throw new KeyNotFoundException($"FantasyPlayer {dto.FantasyPlayerId} not found.");

            _ = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == dto.MatchId, ct)
                ?? throw new KeyNotFoundException($"Match {dto.MatchId} not found.");

            int pts = CalculatePlayerPoints(player.Position,
                dto.Appeared, dto.Goals, dto.Assists, dto.YellowCards, dto.RedCards);

            var existing = await _db.PlayerMatchFantasyStats
                .FirstOrDefaultAsync(s => s.FantasyPlayerId == dto.FantasyPlayerId && s.MatchId == dto.MatchId, ct);

            if (existing != null)
            {
                existing.IsHeAppeard   = dto.Appeared;
                existing.Goals         = dto.Goals;
                existing.Assists       = dto.Assists;
                existing.YellowCards   = dto.YellowCards;
                existing.RedCard       = dto.RedCards;
                existing.FantasyPoints = pts;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.PlayerMatchFantasyStats.Add(new PlayerMatchFantasyStat
                {
                    FantasyPlayerId = dto.FantasyPlayerId,
                    MatchId         = dto.MatchId,
                    IsHeAppeard     = dto.Appeared,
                    Goals           = dto.Goals,
                    Assists         = dto.Assists,
                    YellowCards     = dto.YellowCards,
                    RedCard         = dto.RedCards,
                    FantasyPoints   = pts,
                    CreatedAt       = DateTime.UtcNow,
                    LastUpdatedAt   = DateTime.UtcNow,
                });
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Submitted stats for player {PlayerId} in match {MatchId}: {Pts} pts", dto.FantasyPlayerId, dto.MatchId, pts);
        }

        // ── Admin: calculate weekly scores ────────────────────────────

        public async Task CalculateGameweekScoresAsync(int gameweekId, CancellationToken ct = default)
        {
            var gameweek = await _db.FantasyGameweeks.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == gameweekId, ct)
                ?? throw new KeyNotFoundException("Gameweek not found.");

            // Get all teams that have selections for this gameweek
            var selections = await _db.FantasyTeamSelections.AsNoTracking()
                .Where(s => s.FantasyGameweekId == gameweekId)
                .Include(s => s.FantasyPlayer)
                .ToListAsync(ct);

            // Get all match stats in this gameweek window
            var allStats = await _db.PlayerMatchFantasyStats.AsNoTracking()
                .Where(st => st.Match.MatchDate >= gameweek.StartDate
                          && st.Match.MatchDate <= gameweek.EndDate)
                .ToListAsync(ct);

            var statsMap = allStats.GroupBy(st => st.FantasyPlayerId)
                .ToDictionary(g => g.Key, g => g.Sum(st => st.FantasyPoints));

            var teamGroups = selections.GroupBy(s => s.FantasyTeamId);

            foreach (var group in teamGroups)
            {
                int totalPts = 0;
                foreach (var sel in group)
                {
                    int basePoints = statsMap.GetValueOrDefault(sel.FantasyPlayerId, 0);
                    totalPts += sel.IsCaptain ? basePoints * 2 : basePoints;
                }

                var existing = await _db.FantasyScores
                    .FirstOrDefaultAsync(sc => sc.FantasyTeamId == group.Key && sc.FantasyGameweekId == gameweekId, ct);

                if (existing != null)
                {
                    existing.WeeklyPoints  = totalPts;
                    existing.IsFinalized   = true;
                    existing.LastUpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.FantasyScores.Add(new FantasyScore
                    {
                        FantasyTeamId     = group.Key,
                        FantasyGameweekId = gameweekId,
                        WeeklyPoints      = totalPts,
                        IsFinalized       = true,
                        CreatedAt         = DateTime.UtcNow,
                        LastUpdatedAt     = DateTime.UtcNow,
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
            await _cache.RemoveAsync(LeaderboardKey(gameweekId), ct);
            _logger.LogInformation("Calculated scores for gameweek {GW}: {Count} teams", gameweek.GameWeek, teamGroups.Count());
        }

        // ── Price recalculation ───────────────────────────────────────

        /// <summary>
        /// Recalculate fantasy player prices based on each team's historical
        /// goals scored (last 60 matches). Better attacking teams get pricier
        /// players. Prices are clamped to sensible ranges per position.
        /// </summary>
        public async Task<int> RecalcPlayerPricesAsync(CancellationToken ct = default)
        {
            // 1. Load recent finished matches with scores
            var matches = await _db.Matches
                .AsNoTracking()
                .Where(m => m.Status == "FINISHED" && m.HomeScore != null && m.AwayScore != null)
                .ToListAsync(ct);

            // 2. Compute average goals scored per team
            var goalsFor = new Dictionary<int, (double total, int count)>();
            foreach (var m in matches)
            {
                Accumulate(goalsFor, m.HomeTeamId, m.HomeScore!.Value);
                Accumulate(goalsFor, m.AwayTeamId, m.AwayScore!.Value);
            }

            static void Accumulate(Dictionary<int, (double, int)> d, int id, int goals)
            {
                var (t, c) = d.GetValueOrDefault(id);
                d[id] = (t + goals, c + 1);
            }

            // 3. Turn into avg goals scored per game
            var avgGoals = goalsFor.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.count >= 5 ? kv.Value.total / kv.Value.count : 1.2);

            if (avgGoals.Count == 0) return 0;

            // Normalise 0-1 across all teams
            double minG = avgGoals.Values.Min();
            double maxG = avgGoals.Values.Max();
            double range = maxG - minG < 0.01 ? 1 : maxG - minG;

            double Strength(int teamId) =>
                avgGoals.TryGetValue(teamId, out var g) ? (g - minG) / range : 0.5;

            // Price bands per position: [min, max]
            static (decimal min, decimal max) Band(FantasyPlayer.FantasyPosition pos) => pos switch
            {
                FantasyPlayer.FantasyPosition.GK  => (4.5m, 8.0m),
                FantasyPlayer.FantasyPosition.DEF => (4.5m, 9.0m),
                FantasyPlayer.FantasyPosition.MID => (5.0m, 11.5m),
                FantasyPlayer.FantasyPosition.FWD => (5.5m, 13.0m),
                _ => (5.0m, 9.0m)
            };

            // 4. Update prices
            var players = await _db.FantasyPlayers.Where(p => p.IsActive).ToListAsync(ct);
            foreach (var p in players)
            {
                var s = Strength(p.TeamId);
                var (lo, hi) = Band(p.Position);
                // Round to nearest 0.5
                var raw  = lo + (decimal)s * (hi - lo);
                p.Price  = Math.Round(raw * 2, MidpointRounding.AwayFromZero) / 2;
                p.LastUpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await _cache.RemoveAsync(PlayersKey, ct);
            return players.Count;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static FantasyGameweekResponseDTO MapGameweek(FantasyGameweek gw) => new()
        {
            Id          = gw.Id,
            GameWeek    = gw.GameWeek,
            StartDate   = gw.StartDate,
            EndDate     = gw.EndDate,
            Deadline    = gw.Deadline,
            IsLocked    = gw.IsLocked,
            IsCompleted = gw.IsCompleted,
        };
    }
}

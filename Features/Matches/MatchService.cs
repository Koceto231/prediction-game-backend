using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BPFL.API.Features.Matches
{
    public class PagedResult<T>
    {
        public List<T> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    public class MatchService
    {
        private readonly BPFL_DBContext  bPFL_DBContext;
        private readonly OddsService     oddsService;
        private readonly IAppCache       _cache;
        private readonly HashSet<string> _validLeagueCodes;

        private const string UpcomingCacheKey = "matches:upcoming";
        private static readonly TimeSpan UpcomingTtl = TimeSpan.FromMinutes(1);

        public MatchService(BPFL_DBContext _bPFL_DBContext, OddsService _oddsService,
                            IAppCache cache, IConfiguration configuration)
        {
            bPFL_DBContext = _bPFL_DBContext;
            oddsService   = _oddsService;
            _cache        = cache;

            var codes = configuration
                .GetSection("BackgroundJobs:LeagueCodes")
                .Get<List<string>>() ?? ["BGL", "PL", "BL1", "SA", "PD"];
            _validLeagueCodes = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<PagedResult<MatchDto>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query      = GetMatches();
            var totalCount = await query.CountAsync(ct);
            var items      = await query
                .OrderByDescending(m => m.MatchDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<MatchDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task<MatchDto?> GetByIDMatch(int id, CancellationToken ct = default)
        {
            if (id < 0) return null;
            return await GetMatches().FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<List<MatchDto>> GetMatchesByTeamIdAsync(int id, CancellationToken ct = default)
        {
            if (id < 0) return new List<MatchDto>();
            return await GetMatches()
                .Where(m => m.HomeTeamId == id || m.AwayTeamId == id)
                .OrderByDescending(m => m.MatchDate)
                .ToListAsync(ct);
        }

        public async Task<List<MatchDto>> GetFutureMatches(int take = 20, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);

            var cacheKey = $"{UpcomingCacheKey}:{take}";
            var cached   = await _cache.GetAsync<List<MatchDto>>(cacheKey, ct);
            if (cached != null) return cached;

            await oddsService.EnsureOddsForUpcomingMatchesAsync(ct);

            var validCodes = _validLeagueCodes.ToList();

            var result = await GetMatches()
                .Where(m => m.MatchDate >= DateTime.UtcNow &&
                            m.Status != "FINISHED" &&
                            (m.LeagueCode != null
                                ? validCodes.Contains(m.LeagueCode)
                                // Fallback for older rows without LeagueCode:
                                // both teams must be from the same known league
                                : bPFL_DBContext.Teams.Any(t => t.Id == m.HomeTeamId && t.LeagueCode != null &&
                                  validCodes.Contains(t.LeagueCode) &&
                                  bPFL_DBContext.Teams.Any(t2 => t2.Id == m.AwayTeamId && t2.LeagueCode == t.LeagueCode))))
                .OrderBy(m => m.MatchDate)
                .Take(take)
                .ToListAsync(ct);

            await _cache.SetAsync(cacheKey, result, UpcomingTtl, ct);
            return result;
        }

        public Task InvalidateUpcomingCacheAsync(CancellationToken ct = default)
            => _cache.RemoveAsync($"{UpcomingCacheKey}:20", ct);

        private IQueryable<MatchDto> GetMatches()
        {
            return bPFL_DBContext.Matches.AsNoTracking().Select(m => new MatchDto
            {
                Id           = m.Id,
                MatchDate    = m.MatchDate,
                Status       = m.Status,
                MatchDay     = m.MatchDay,
                HomeTeamId   = m.HomeTeamId,
                HomeTeamName = m.HomeTeam.Name,
                AwayTeamId   = m.AwayTeamId,
                AwayTeamName = m.AwayTeam.Name,
                HomeScore    = m.HomeScore,
                AwayScore    = m.AwayScore,
                HomeOdds     = m.HomeOdds,
                DrawOdds     = m.DrawOdds,
                AwayOdds     = m.AwayOdds,
                LeagueCode   = m.LeagueCode
            });
        }
    }
}

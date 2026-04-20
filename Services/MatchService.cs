using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
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
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly OddsService oddsService;

        public MatchService(BPFL_DBContext _bPFL_DBContext, OddsService _oddsService)
        {
            bPFL_DBContext = _bPFL_DBContext;
            oddsService = _oddsService;
        }

        public async Task<PagedResult<MatchDto>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = GetMatches();

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(m => m.MatchDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<MatchDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MatchDto?> GetByIDMatch(int id, CancellationToken ct = default)
        {

            if (id < 0)
            {
                return null;
            }

            return await GetMatches().FirstOrDefaultAsync(x => x.Id == id,ct);
        }

        public async Task<List<MatchDto>> GetMatchesByTeamIdAsync(int id, CancellationToken ct = default)
        {

            if(id < 0)
            {
                return new List<MatchDto>();
            }

            return await GetMatches().Where(m => m.HomeTeamId == id || m.AwayTeamId == id)
           .OrderByDescending(m => m.MatchDate).ToListAsync(ct);
    
        }

        public async Task<List<MatchDto>> GetFutureMatches(int take = 20, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);

            await oddsService.EnsureOddsForUpcomingMatchesAsync(ct);

            return await GetMatches().Where(m => m.MatchDate >= DateTime.UtcNow && m.Status != "FINISHED")
                .OrderBy(m => m.MatchDate)
                .Take(take).ToListAsync(ct);
        }

          private IQueryable<MatchDto> GetMatches()
        {
            return bPFL_DBContext.Matches.AsNoTracking().Select(m => new MatchDto
            {
                Id = m.Id,
                MatchDate = m.MatchDate,
                Status = m.Status,
                MatchDay = m.MatchDay,

                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam.Name,

                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam.Name,

                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,

                HomeOdds = m.HomeOdds,
                DrawOdds = m.DrawOdds,
                AwayOdds = m.AwayOdds
            });
        }
        }
    }


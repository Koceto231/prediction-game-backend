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

        public MatchService(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }

        public async Task<PagedResult<MatchDto>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = GetMatches();
            var countTask = query.CountAsync(ct);

            var itemsTask = query
                            .OrderByDescending(m => m.MatchDate)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(ct);

            await Task.WhenAll(countTask, itemsTask);

            return new PagedResult<MatchDto>
            {
                Items = itemsTask.Result,
                TotalCount = countTask.Result,
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

            return await GetMatches().Where(m => m.MatchDate >= DateTime.Now && m.Status != "FINISHED")
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
                AwayScore = m.AwayScore
            });
        }
        }
    }


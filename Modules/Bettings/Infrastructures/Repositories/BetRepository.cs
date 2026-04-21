using BPFL.API.Data;
using BPFL.API.Modules.Bettings.Application.Interfaces;
using BPFL.API.Modules.Bettings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.Bettings.Infrastructures.Repositories
{
    public class BetRepository : IBetRepository
    {

        private readonly BPFL_DBContext bPFL_DBContext;

        public BetRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }
        public async Task AddAsync(Bet bet, CancellationToken ct = default)
        {
           await bPFL_DBContext.AddAsync(bet, ct);
        }

        public async Task<List<Bet>> GetOpenBetsByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await bPFL_DBContext.Bets.Where(k => k.UserId == userId && k.Status == Domain.Enums.BetStatus.Pending)
                .AsNoTracking().ToListAsync(ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await bPFL_DBContext.SaveChangesAsync(ct);
        }
    }
}

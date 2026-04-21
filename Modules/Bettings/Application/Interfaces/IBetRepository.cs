using BPFL.API.Modules.Bettings.Domain.Entities;

namespace BPFL.API.Modules.Bettings.Application.Interfaces
{
    public interface IBetRepository
    {
        Task AddAsync(Bet bet, CancellationToken ct = default);

        Task<List<Bet>> GetOpenBetsByUserIdAsync(int userId, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

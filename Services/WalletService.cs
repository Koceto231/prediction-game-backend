using BPFL.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class WalletService
    {
        private readonly BPFL_DBContext _db;
        private readonly ILogger<WalletService> _logger;

        private const decimal TopUpAmount = 1000m;

        public WalletService(BPFL_DBContext db, ILogger<WalletService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<decimal> GetBalanceAsync(int userId, CancellationToken ct = default)
        {
            var balance = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Balance)
                .FirstOrDefaultAsync(ct);

            return balance;
        }

        public async Task<decimal> TopUpAsync(int userId, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                throw new KeyNotFoundException("User not found.");

            user.Balance += TopUpAmount;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} topped up. New balance: {Balance}", userId, user.Balance);

            return user.Balance;
        }
    }
}

using BPFL.API.Models;

namespace BPFL.API.Modules.Wallet.Domain.Entities
{
    public class Wallet
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public decimal Balance { get; set; }

        public decimal StartingBalance { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}

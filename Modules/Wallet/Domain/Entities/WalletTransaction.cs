namespace BPFL.API.Modules.Wallet.Domain.Entities
{
    public class WalletTransaction
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public decimal Amount { get; set; }

        public string Type { get; set; } = null!; 

        public string Description { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public Models.User User { get; set; } = null!;
    }
}

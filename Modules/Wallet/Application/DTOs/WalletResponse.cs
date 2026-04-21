namespace BPFL.API.Modules.Wallet.Applications.DTOs
{
    public class WalletResponse
    {
        public decimal Balance { get; set; }

        public decimal StartingBalance { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}

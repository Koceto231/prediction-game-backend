using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20260427120000_CreateWalletsIfMissing")]
    public partial class CreateWalletsIfMissing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Wallets"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                    ""Balance"" NUMERIC NOT NULL DEFAULT 0,
                    ""StartingBalance"" NUMERIC NOT NULL DEFAULT 0,
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Wallets_UserId"" ON ""Wallets""(""UserId"");

                CREATE TABLE IF NOT EXISTS ""WalletTransactions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                    ""Amount"" NUMERIC NOT NULL,
                    ""Type"" TEXT NOT NULL,
                    ""Description"" TEXT NOT NULL,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_WalletTransactions_UserId"" ON ""WalletTransactions""(""UserId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""WalletTransactions"";
                DROP TABLE IF EXISTS ""Wallets"";
            ");
        }
    }
}

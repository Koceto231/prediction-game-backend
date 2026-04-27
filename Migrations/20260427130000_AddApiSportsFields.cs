using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    public partial class AddApiSportsFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Matches""
                    ADD COLUMN IF NOT EXISTS ""ApiSportsFixtureId"" INTEGER,
                    ADD COLUMN IF NOT EXISTS ""LeagueCode"" TEXT;

                ALTER TABLE ""FantasyPlayers""
                    ADD COLUMN IF NOT EXISTS ""ApiSportsPlayerId"" INTEGER;

                CREATE INDEX IF NOT EXISTS ""IX_FantasyPlayers_ApiSportsPlayerId""
                    ON ""FantasyPlayers""(""ApiSportsPlayerId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Matches""
                    DROP COLUMN IF EXISTS ""ApiSportsFixtureId"",
                    DROP COLUMN IF EXISTS ""LeagueCode"";

                ALTER TABLE ""FantasyPlayers""
                    DROP COLUMN IF EXISTS ""ApiSportsPlayerId"";
            ");
        }
    }
}

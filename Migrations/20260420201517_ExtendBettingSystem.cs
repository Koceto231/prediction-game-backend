using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class ExtendBettingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ExpectedAwayGoals",
                table: "Matches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedHomeGoals",
                table: "Matches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Pick",
                table: "Bets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "BTTSPick",
                table: "Bets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BetType",
                table: "Bets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OULine",
                table: "Bets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OUPick",
                table: "Bets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreAway",
                table: "Bets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreHome",
                table: "Bets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedAwayGoals",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ExpectedHomeGoals",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BTTSPick",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "BetType",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "OULine",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "OUPick",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "ScoreAway",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "ScoreHome",
                table: "Bets");

            migrationBuilder.AlterColumn<int>(
                name: "Pick",
                table: "Bets",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}

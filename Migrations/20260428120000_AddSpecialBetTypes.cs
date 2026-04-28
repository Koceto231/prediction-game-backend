using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialBetTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Bets table: new columns for special bet types ─────────────

            // Goalscorer bet — stores FantasyPlayer.Id
            migrationBuilder.AddColumn<int>(
                name: "GoalscorerId",
                table: "Bets",
                type: "integer",
                nullable: true);

            // Corners / Yellow Cards: flexible numeric line (e.g. 9.5, 3.5)
            migrationBuilder.AddColumn<decimal>(
                name: "LineValue",
                table: "Bets",
                type: "numeric",
                nullable: true);

            // Double Chance pick (1=HomeOrDraw, 2=HomeOrAway, 3=DrawOrAway)
            migrationBuilder.AddColumn<int>(
                name: "DCPick",
                table: "Bets",
                type: "integer",
                nullable: true);

            // ── Matches table: match-level stats for bet resolution ───────

            migrationBuilder.AddColumn<int>(
                name: "TotalCorners",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalYellowCards",
                table: "Matches",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GoalscorerId",    table: "Bets");
            migrationBuilder.DropColumn(name: "LineValue",        table: "Bets");
            migrationBuilder.DropColumn(name: "DCPick",           table: "Bets");
            migrationBuilder.DropColumn(name: "TotalCorners",     table: "Matches");
            migrationBuilder.DropColumn(name: "TotalYellowCards", table: "Matches");
        }
    }
}

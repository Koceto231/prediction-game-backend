using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class SyncPredictionSchema1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        
          

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Predictions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_LeagueId",
                table: "Predictions",
                column: "LeagueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_Leagues_LeagueId",
                table: "Predictions",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

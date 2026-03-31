using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class MakeScoreOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PredictionHomeScore",
                table: "Predictions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PredictionAwayScore",
                table: "Predictions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "ActualOUResult",
                table: "Predictions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualWinner",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BonusPointsOU",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BonusPointsWinner",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrectBTTS",
                table: "Predictions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsFromBTTS",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PredictionBTTS",
                table: "Predictions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredictionOULine",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredictionOUPick",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredictionWinner",
                table: "Predictions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualOUResult",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ActualWinner",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "BonusPointsOU",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "BonusPointsWinner",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "IsCorrectBTTS",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PointsFromBTTS",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PredictionBTTS",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PredictionOULine",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PredictionOUPick",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PredictionWinner",
                table: "Predictions");

            migrationBuilder.AlterColumn<int>(
                name: "PredictionHomeScore",
                table: "Predictions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PredictionAwayScore",
                table: "Predictions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}

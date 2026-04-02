using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Users ---
            // Login lookups always filter by Email
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // Email verification token lookup
            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailVerificationToken",
                table: "Users",
                column: "EmailVerificationToken",
                unique: true,
                filter: "\"EmailVerificationToken\" IS NOT NULL");

            // Password reset token lookup
            migrationBuilder.CreateIndex(
                name: "IX_Users_PasswordResetToken",
                table: "Users",
                column: "PasswordResetToken",
                unique: true,
                filter: "\"PasswordResetToken\" IS NOT NULL");

            // --- RefreshTokens ---
            // Every refresh/revoke hashes the token and does a WHERE TokenHash = ?
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            // Revoking all tokens for a user on password reset: WHERE UserId = ? AND RevokedAt IS NULL
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_RevokedAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "RevokedAt" });

            // --- Matches ---
            // MatchAnalysisService queries by HomeTeamId/AwayTeamId + Status + MatchDate (3 separate queries)
            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId_Status_MatchDate",
                table: "Matches",
                columns: new[] { "HomeTeamId", "Status", "MatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId_Status_MatchDate",
                table: "Matches",
                columns: new[] { "AwayTeamId", "Status", "MatchDate" });

            // GetFutureMatches: WHERE MatchDate >= NOW AND Status != 'FINISHED'
            migrationBuilder.CreateIndex(
                name: "IX_Matches_MatchDate_Status",
                table: "Matches",
                columns: new[] { "MatchDate", "Status" });

            // MatchSyncService: WHERE ExternalId IN (...)
            migrationBuilder.CreateIndex(
                name: "IX_Matches_ExternalId",
                table: "Matches",
                column: "ExternalId",
                unique: true);

            // --- Predictions ---
            // GetMyPredictions, CreatePrediction duplicate check, UpdatePrediction
            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_MatchId",
                table: "Predictions",
                columns: new[] { "UserId", "MatchId" },
                unique: true); // one prediction per user per match

            // ScoreMatchPredictions: WHERE MatchId = ? AND Points IS NULL
            migrationBuilder.CreateIndex(
                name: "IX_Predictions_MatchId_Points",
                table: "Predictions",
                columns: new[] { "MatchId", "Points" });

            // Leaderboard: GROUP BY UserId, SUM Points — covers both global and league leaderboard
            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_Points",
                table: "Predictions",
                columns: new[] { "UserId", "Points" });

            // PredictionScoringJob: WHERE Points IS NULL AND Match.Status = 'FINISHED'
            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Points",
                table: "Predictions",
                column: "Points",
                filter: "\"Points\" IS NULL");

            // --- Teams ---
            // TeamSyncService: WHERE ExternalId IN (...)
            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalId",
                table: "Teams",
                column: "ExternalId",
                unique: true);

            // --- LeagueMembers ---
            // JoinLeague: WHERE UserId = ? AND LeagueId = ?
            // LeaveLeague: WHERE UserId = ? AND LeagueId = ?
            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_UserId_LeagueId",
                table: "LeagueMembers",
                columns: new[] { "UserId", "LeagueId" },
                unique: true);

            // GetMyLeagues: WHERE Members.Any(m => m.UserId = ?)
            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_LeagueId",
                table: "LeagueMembers",
                column: "LeagueId");

            // --- Leagues ---
            // JoinLeague: WHERE InviteCode = ?
            migrationBuilder.CreateIndex(
                name: "IX_Leagues_InviteCode",
                table: "Leagues",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_Users_Email", "Users");
            migrationBuilder.DropIndex("IX_Users_EmailVerificationToken", "Users");
            migrationBuilder.DropIndex("IX_Users_PasswordResetToken", "Users");
            migrationBuilder.DropIndex("IX_RefreshTokens_TokenHash", "RefreshTokens");
            migrationBuilder.DropIndex("IX_RefreshTokens_UserId_RevokedAt", "RefreshTokens");
            migrationBuilder.DropIndex("IX_Matches_HomeTeamId_Status_MatchDate", "Matches");
            migrationBuilder.DropIndex("IX_Matches_AwayTeamId_Status_MatchDate", "Matches");
            migrationBuilder.DropIndex("IX_Matches_MatchDate_Status", "Matches");
            migrationBuilder.DropIndex("IX_Matches_ExternalId", "Matches");
            migrationBuilder.DropIndex("IX_Predictions_UserId_MatchId", "Predictions");
            migrationBuilder.DropIndex("IX_Predictions_MatchId_Points", "Predictions");
            migrationBuilder.DropIndex("IX_Predictions_UserId_Points", "Predictions");
            migrationBuilder.DropIndex("IX_Predictions_Points", "Predictions");
            migrationBuilder.DropIndex("IX_Teams_ExternalId", "Teams");
            migrationBuilder.DropIndex("IX_LeagueMembers_UserId_LeagueId", "LeagueMembers");
            migrationBuilder.DropIndex("IX_LeagueMembers_LeagueId", "LeagueMembers");
            migrationBuilder.DropIndex("IX_Leagues_InviteCode", "Leagues");
        }
    }
}

using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.MatchAnalysis;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class MatchAnalysisService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<MatchAnalysisService> _logger;

        public MatchAnalysisService(BPFL_DBContext _bPFL_DBContext, ILogger<MatchAnalysisService> logger)
        {
            bPFL_DBContext = _bPFL_DBContext;
            _logger = logger;
        }

        public async Task<MatchAnalysisDTO> AnalyzeMatch(Match match, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(match);

            if (match.HomeTeam == null || match.AwayTeam == null)
                throw new ArgumentException("Match must include HomeTeam and AwayTeam navigation properties.");

            _logger.LogDebug("Analyzing match: {HomeTeam} vs {AwayTeam}",
                match.HomeTeam.Name, match.AwayTeam.Name);

            int homeTeamId = match.HomeTeamId;
            int awayTeamId = match.AwayTeamId;


            var homeLastMatches = await GetLatestMatchesAsync(homeTeamId, ct);
            var awayLastMatches = await GetLatestMatchesAsync(awayTeamId, ct);
            var homeHomeMatches = await GetLastHomeMatchesAsync(homeTeamId, ct);
            var awayAwayMatches = await GetLastAwayMatchesAsync(awayTeamId, ct);

            int homeForm = CalculateFormPoints(homeLastMatches, homeTeamId);
            int awayForm = CalculateFormPoints(awayLastMatches, awayTeamId);

            double avgHomeScored = CalculateAverageGoalsScored(homeLastMatches, homeTeamId);
            double avgAwayScored = CalculateAverageGoalsScored(awayLastMatches, awayTeamId);
            double homeScoredAtHome = CalculateAverageGoalsScored(homeHomeMatches, homeTeamId);
            double awayScoredAtAway = CalculateAverageGoalsScored(awayAwayMatches, awayTeamId);
            double avgHomeConceded = CalculateAverageGoalsConceded(homeLastMatches, homeTeamId);
            double avgAwayConceded = CalculateAverageGoalsConceded(awayLastMatches, awayTeamId);

            var result = new MatchAnalysisDTO
            {
                HomeTeam = match.HomeTeam.Name,
                AwayTeam = match.AwayTeam.Name,
                MatchDate = match.MatchDate,

                HomeRecentFromPoints = homeForm,
                HomeAverageGoalsAtHome = homeScoredAtHome,
                HomeAverageGoalsConceded = avgHomeConceded,
                HomeAverageGoalsScored = avgHomeScored,

                AwayRecentFromPoints = awayForm,
                AwayAverageGoalsAtAway = awayScoredAtAway,
                AwayAverageGoalsConceded = avgAwayConceded,
                AwayAverageGoalsScored = avgAwayScored
            };

            _logger.LogInformation(
                "Match analysis completed for {HomeTeam} vs {AwayTeam}. HomeForm={HomeForm}, AwayForm={AwayForm}, HomeAvgScored={HomeScored}, AwayAvgScored={AwayScored}",
                result.HomeTeam, result.AwayTeam,
                result.HomeRecentFromPoints, result.AwayRecentFromPoints,
                result.HomeAverageGoalsScored, result.AwayAverageGoalsScored);

            return result;
        }

      
        private async Task<List<Match>> GetLatestMatchesAsync(int teamId, CancellationToken ct)
        {
            return await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => (x.HomeTeamId == teamId || x.AwayTeamId == teamId) && x.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);
        }

        private async Task<List<Match>> GetLastHomeMatchesAsync(int teamId, CancellationToken ct)
        {
            return await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => x.HomeTeamId == teamId && x.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);
        }

        private async Task<List<Match>> GetLastAwayMatchesAsync(int teamId, CancellationToken ct)
        {
            return await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => x.AwayTeamId == teamId && x.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);
        }

        public int CalculateFormPoints(List<Match> matches, int teamId)
        {
            int points = 0;
            foreach (var match in matches)
            {
                if (match.HomeTeamId == teamId)
                {
                    if (match.HomeScore > match.AwayScore) points += 3;
                    else if (match.HomeScore == match.AwayScore) points++;
                }
                else if (match.AwayTeamId == teamId)
                {
                    if (match.AwayScore > match.HomeScore) points += 3;
                    else if (match.HomeScore == match.AwayScore) points++;
                }
            }
            return points;
        }

        // League-average fallback — used when a team has no finished matches yet
        private const double LeagueAvgGoalsScored   = 1.35;
        private const double LeagueAvgGoalsConceded = 1.35;

        public double CalculateAverageGoalsScored(List<Match> matches, int teamId)
        {
            if (matches == null || matches.Count == 0) return LeagueAvgGoalsScored;

            double goals = matches.Sum(match =>
                match.HomeTeamId == teamId ? (double)(match.HomeScore ?? 0) :
                match.AwayTeamId == teamId ? (double)(match.AwayScore ?? 0) : 0);

            return Math.Round(goals / matches.Count, 2);
        }

        public double CalculateAverageGoalsConceded(List<Match> matches, int teamId)
        {
            if (matches == null || matches.Count == 0) return LeagueAvgGoalsConceded;

            double goals = matches.Sum(match =>
                match.HomeTeamId == teamId ? (double)(match.AwayScore ?? 0) :
                match.AwayTeamId == teamId ? (double)(match.HomeScore ?? 0) : 0);

            return Math.Round(goals / matches.Count, 2);
        }
    }

   

}

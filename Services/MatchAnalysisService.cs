using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.MatchAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

        public async Task<List<Match>> GetLatestMatches(int teamId, CancellationToken ct = default)
        {
            var result = await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => x.HomeTeamId == teamId || x.AwayTeamId == teamId)
                .Where(k => k.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);

            return result;
        } 

        public async Task<List<Match>> GetLastHomeMatches(int teamId, CancellationToken ct = default)
        {

            var result = await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => x.HomeTeamId == teamId)
                .Where(k => k.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);

            return result;
        }

        public async Task<List<Match>> GetLastAwayMatches(int teamId, CancellationToken ct = default)
        {

            var result = await bPFL_DBContext.Matches.AsNoTracking()
                .Where(x => x.AwayTeamId == teamId)
                .Where(k => k.Status == "FINISHED")
                .OrderByDescending(l => l.MatchDate)
                .Take(5)
                .ToListAsync(ct);

            return result;
        }

        public int CalculateFormPoints(List<Match> matches, int teamId)
        {
            int points = 0;

            foreach(var match in matches)
            {

                if (match.HomeTeamId == teamId)
                {
                    if(match.HomeScore > match.AwayScore)
                    {
                        points += 3;
                    }
                    if (match.HomeScore == match.AwayScore)
                    {
                        points++;
                    }
                }
                else if(match.AwayTeamId == teamId)
                {
                    if (match.AwayScore > match.HomeScore)
                    {
                        points += 3;
                    }
                    if (match.HomeScore == match.AwayScore)
                    {
                        points++;
                    }
                }
            }

            return points;
            
        }

        public double CalculateAverageGoalsScored(List<Match> matches, int teamId)
        {
            double goals = 0;

            if (matches == null || matches.Count == 0)
                return 0;

            foreach (var match in matches)
            {
                if (matches.Count == 0)
                {
                    return 0;
                }

                if (match.HomeTeamId == teamId)
                {
                    goals+= (double)match.HomeScore;

                }else if (match.AwayTeamId == teamId)
                {
                    goals += (double)match.AwayScore;
                }
            }

            double averageGoals = goals / matches.Count;

            return Math.Round(averageGoals,2 );
        }

        public double CalculateAverageGoalsConceded(List<Match> matches, int teamId)
        {
            double goals = 0;

            if(matches == null || matches.Count == 0)
                return 0;

            foreach (var match in matches)
            {

                if (match.HomeTeamId == teamId)
                {
                    goals += (double)match.AwayScore;
                }
                else if (match.AwayTeamId == teamId)
                {
                    goals += (double)match.HomeScore;
                }
            }

            double averageGoals = goals / matches.Count;

            return Math.Round(averageGoals, 2);
        }

        public async Task<MatchAnalysisDTO> AnalyzeMatch(Match match, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(match);

            if (match.HomeTeam == null || match.AwayTeam == null) 
            { 
                throw new ArgumentException("Match must include HomeTeam and AwayTeam navigation properties.");
            }

            _logger.LogDebug("Analyzing match: {HomeTeam} vs {AwayTeam}",
                match.HomeTeam.Name, match.AwayTeam.Name);



            string HomeTeamName = match.HomeTeam.Name;
            string AwayTeamName = match.AwayTeam.Name;
             
           int HomeTeamId = match.HomeTeamId;
           int AwayTeamId = match.AwayTeamId;

            var HomeTeamLastMatches = await GetLatestMatches(HomeTeamId,ct);
            var AwayTeamLastMatches = await GetLatestMatches(AwayTeamId, ct);

            var HomeTeamLastHomeMatches = await GetLastHomeMatches(HomeTeamId, ct);
            var AwayTeamLastAwayMatches = await GetLastAwayMatches(AwayTeamId, ct);

            int HomeTeamFor = CalculateFormPoints(HomeTeamLastMatches, HomeTeamId);
            int AwayTeamFor = CalculateFormPoints(AwayTeamLastMatches, AwayTeamId);

            double AverageHomeScored = CalculateAverageGoalsScored(HomeTeamLastMatches, HomeTeamId);
            double AverageAwayScored = CalculateAverageGoalsScored(AwayTeamLastMatches, AwayTeamId);

            double HomeScoredAtHome = CalculateAverageGoalsScored(HomeTeamLastHomeMatches, HomeTeamId);
            double AwayScoredAtAway = CalculateAverageGoalsScored(AwayTeamLastAwayMatches, AwayTeamId);

            double AverageHomeConceded = CalculateAverageGoalsConceded(HomeTeamLastMatches, HomeTeamId);
            double AverageAwayConceded = CalculateAverageGoalsConceded(AwayTeamLastMatches, AwayTeamId);

            var result = new MatchAnalysisDTO
            {
                HomeTeam = HomeTeamName,
                AwayTeam = AwayTeamName,
                MatchDate = match.MatchDate,

                HomeRecentFromPoints = HomeTeamFor,
                HomeAverageGoalsAtHome = HomeScoredAtHome,
                HomeAverageGoalsConceded = AverageHomeConceded,
                HomeAverageGoalsScored = AverageHomeScored,

                AwayRecentFromPoints = AwayTeamFor,
                AwayAverageGoalsAtAway = AwayScoredAtAway,
                AwayAverageGoalsConceded = AverageAwayConceded,
                AwayAverageGoalsScored = AverageAwayScored
            };

            _logger.LogInformation(
                "Match analysis completed for {HomeTeam} vs {AwayTeam}. HomeForm={HomeForm}, AwayForm={AwayForm}, HomeAvgScored={HomeScored}, AwayAvgScored={AwayScored}",
                result.HomeTeam,
                result.AwayTeam,
                result.HomeRecentFromPoints,
                result.AwayRecentFromPoints,
                result.HomeAverageGoalsScored,
                result.AwayAverageGoalsScored);

            return result;
        }
    }
    }


namespace BPFL.API.Modules.AI.Domain.Entities
{
    public class MatchFeatureSet
    {

        public int MatchId { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public string HomeTeamName { get; set; } = null!;
        public string AwayTeamName { get; set; } = null!;

        public double HomeFormScore { get; set; }
        public double AwayFormScore { get; set; }

        public double HomeAttackStrength { get; set; }
        public double AwayAttackStrength { get; set; }

        public double HomeDefenseStrength { get; set; }
        public double AwayDefenseStrength { get; set; }

        public double HomeGoalsScoredAvg { get; set; }
        public double AwayGoalsScoredAvg { get; set; }

        public double HomeGoalsConcededAvg { get; set; }
        public double AwayGoalsConcededAvg { get; set; }

        public double HomeBTTSRate { get; set; }
        public double AwayBTTSRate { get; set; }

        public double HomeOver25Rate { get; set; }
        public double AwayOver25Rate { get; set; }

        public double HomeWinRate { get; set; }
        public double AwayWinRate { get; set; }

        public double DrawRateSignal { get; set; }

        public double HomeAdvantage { get; set; }
    }
}

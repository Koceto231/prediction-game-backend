namespace BPFL.API.Models
{
    public class Predictionenums
    {
        public enum MatchWinner
        {
            Home = 1,
            Draw = 2,
            Away = 3
        }

        public enum OverUnderLine
        {
            Line15 = 1,   
            Line25 = 2,   
            Line35 = 3   
        }

        public enum OverUnderPick
        {
            Over = 1,
            Under = 2
        }

        // Double Chance: 1X = home or draw, HomeOrAway = either team wins (no draw), X2 = draw or away
        public enum DoubleChancePick
        {
            HomeOrDraw = 1,
            HomeOrAway = 2,
            DrawOrAway = 3
        }
    }
}

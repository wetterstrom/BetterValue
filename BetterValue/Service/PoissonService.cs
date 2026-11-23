using BetterValue.Models;

namespace BetterValue.Service;

public interface IPoissonService
{
    MatchProbabilities CalculateProbabilities(
        Team homeTeam,
        Team awayTeam,
        double leagueHomeAvg = 1.55,
        double leagueAwayAvg = 1.25);
}

public record MatchProbabilities(
    double HomeWin,
    double Draw,
    double AwayWin,
    double ExpectedHomeGoals,
    double ExpectedAwayGoals,
    double Over25,
    double Under25,
    double Over35,
    double Under35
);

public class PoissonService : IPoissonService
{
    private double CalculatePoisson(int k, double lambda)
    {
        return Math.Exp(-lambda) * Math.Pow(lambda, k) / Factorial(k);
    }

    private double Factorial(int n)
    {
        if (n <= 1) return 1;
        double result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }

    public MatchProbabilities CalculateProbabilities(
        Team homeTeam,
        Team awayTeam,
        double leagueHomeAvg = 1.55,
        double leagueAwayAvg = 1.25)
    {
        // Multiplikativ Poisson (hemmafördelen finns redan inbakad i lagens Hemma/Borta-värden)
        double homeXg = (homeTeam.HomeGoalsScoredAvg * awayTeam.AwayGoalsConcededAvg) / leagueHomeAvg;
        double awayXg = (awayTeam.AwayGoalsScoredAvg * homeTeam.HomeGoalsConcededAvg) / leagueAwayAvg;

        // Skydd mot orimligt låga värden om ett lag ännu inte släppt in mål
        homeXg = Math.Max(0.15, homeXg);
        awayXg = Math.Max(0.15, awayXg);

        double homeWinProb = 0;
        double drawProb = 0;
        double awayWinProb = 0;

        double over25Prob = 0, under25Prob = 0;
        double over35Prob = 0, under35Prob = 0;

        for (int homeGoals = 0; homeGoals <= 10; homeGoals++)
        {
            for (int awayGoals = 0; awayGoals <= 10; awayGoals++)
            {
                double matchScoreProb = CalculatePoisson(homeGoals, homeXg) * CalculatePoisson(awayGoals, awayXg);
                int totalGoals = homeGoals + awayGoals;

                // 1X2
                if (homeGoals > awayGoals) homeWinProb += matchScoreProb;
                else if (homeGoals == awayGoals) drawProb += matchScoreProb;
                else awayWinProb += matchScoreProb;

                // Över/Under 2.5
                if (totalGoals > 2) over25Prob += matchScoreProb;
                else under25Prob += matchScoreProb;

                // Över/Under 3.5
                if (totalGoals > 3) over35Prob += matchScoreProb;
                else under35Prob += matchScoreProb;
            }
        }

        double total = homeWinProb + drawProb + awayWinProb;

        return new MatchProbabilities(
            HomeWin: homeWinProb / total,
            Draw: drawProb / total,
            AwayWin: awayWinProb / total,
            ExpectedHomeGoals: homeXg,
            ExpectedAwayGoals: awayXg,
            Over25: over25Prob / total,
            Under25: under25Prob / total,
            Over35: over35Prob / total,
            Under35: under35Prob / total
        );
    }
}
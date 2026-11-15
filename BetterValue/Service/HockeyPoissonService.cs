using BetterValue.Models;
using System;

namespace BetterValue.Service;

public class HockeyMatchProbabilities
{
    public double HomeWinReg { get; set; } // 1 (Full tid)
    public double DrawReg { get; set; }    // X (Full tid)
    public double AwayWinReg { get; set; } // 2 (Full tid)

    public double HomeMoneyline { get; set; } // Inkl. OT/Straffar
    public double AwayMoneyline { get; set; } // Inkl. OT/Straffar

    public double Over55 { get; set; } // Över 5.5 mål
    public double Under55 { get; set; } // Under 5.5 mål
}

public class HockeyPoissonService
{
    public HockeyMatchProbabilities CalculateProbabilities(HockeyDataMatch home, HockeyDataMatch away)
    {
        // Beräkna xG per match (Eftersom datan är för hela säsongen)
        double homeAttack = home.xGoalsFor / Math.Max(1, home.games_played);
        double awayDefense = away.xGoalsAgainst / Math.Max(1, away.games_played);

        double awayAttack = away.xGoalsFor / Math.Max(1, away.games_played);
        double homeDefense = home.xGoalsAgainst / Math.Max(1, home.games_played);

        // Förväntade mål (Enkel snittning + 4% Hemmafördel i Hockey)
        double homeExpGoals = ((homeAttack + awayDefense) / 2.0) * 1.04;
        double awayExpGoals = ((awayAttack + homeDefense) / 2.0) * 0.96;

        var probs = new HockeyMatchProbabilities();

        // Simulera Poisson upp till 10 mål (Ishockey är målrikt)
        for (int h = 0; h <= 10; h++)
        {
            for (int a = 0; a <= 10; a++)
            {
                double prob = Poisson(h, homeExpGoals) * Poisson(a, awayExpGoals);

                if (h > a) probs.HomeWinReg += prob;
                else if (a > h) probs.AwayWinReg += prob;
                else probs.DrawReg += prob;

                if ((h + a) > 5.5) probs.Over55 += prob;
                else probs.Under55 += prob;
            }
        }

        // Moneyline: Fördela sannolikheten för oavgjort (50/50 i OT/Straffar som standard)
        probs.HomeMoneyline = probs.HomeWinReg + (probs.DrawReg * 0.50);
        probs.AwayMoneyline = probs.AwayWinReg + (probs.DrawReg * 0.50);

        return probs;
    }

    private double Poisson(int k, double lambda)
    {
        return (Math.Pow(lambda, k) * Math.Exp(-lambda)) / Factorial(k);
    }

    private long Factorial(int n)
    {
        if (n <= 1) return 1;
        long result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }
}
using BetterValue.Models;
using System;

namespace BetterValue.Service;

public record HockeyLeagueAverages(
    double AllAvg,
    double EsAvg,
    double PpAvg,
    double OtherAvg
);

public class HockeyMatchProbabilities
{
    public double ExpectedHomeGoals { get; set; }
    public double ExpectedAwayGoals { get; set; }

    public double HomeWinReg { get; set; } // 1 (60 min)
    public double DrawReg { get; set; }    // X (60 min)
    public double AwayWinReg { get; set; } // 2 (60 min)

    public double HomeMoneyline { get; set; } // Inkl. OT/Straffar
    public double AwayMoneyline { get; set; } // Inkl. OT/Straffar

    public double HomePucklineMinus15 { get; set; } // Hemma -1.5 (vinst med 2+ mål)
    public double AwayPucklineMinus15 { get; set; } // Borta -1.5 (vinst med 2+ mål)

    public double Over55 { get; set; }
    public double Under55 { get; set; }
    public double Over65 { get; set; }
    public double Under65 { get; set; }
}

public class HockeyPoissonService
{
    public HockeyLeagueAverages CalculateLeagueAverages(IEnumerable<HockeyDataMatch> teams)
    {
        var list = teams.Where(t => t.games_played > 0).ToList();
        if (!list.Any())
            return new HockeyLeagueAverages(3.05, 2.10, 0.58, 0.37);

        double allAvg = list.Average(t => (t.AllAttackPerGame + t.AllDefensePerGame) / 2.0);
        if (allAvg <= 0) allAvg = 3.05;

        var withBreakdown = list.Where(t => t.HasSituationBreakdown && t.EsAttackPerGame > 0).ToList();
        if (!withBreakdown.Any())
            return new HockeyLeagueAverages(allAvg, 0, 0, 0);

        double esAvg = withBreakdown.Average(t => (t.EsAttackPerGame + t.EsDefensePerGame) / 2.0);
        double ppAvg = withBreakdown.Average(t => (t.PpAttackPerGame + t.PkDefensePerGame) / 2.0);

        // Resterande mål (4v4, SHG, 6v5/EN) så totalsnittet exakt matchar ligan
        double otherAvg = Math.Max(0.0, allAvg - esAvg - ppAvg);

        return new HockeyLeagueAverages(allAvg, esAvg, ppAvg, otherAvg);
    }

    public HockeyMatchProbabilities CalculateProbabilities(
        HockeyDataMatch home,
        HockeyDataMatch away,
        HockeyLeagueAverages? league = null,
        double homeGoalieFactor = 1.0,
        double awayGoalieFactor = 1.0,
        bool homeBackToBack = false,
        bool awayBackToBack = false)
    {
        league ??= new HockeyLeagueAverages(3.05, 2.10, 0.58, 0.37);

        double homeExpGoals;
        double awayExpGoals;

        // Multiplikativ 5v5 + Special Teams om MoneyPuck-uppdelningen finns
        bool useBreakdown = home.HasSituationBreakdown && away.HasSituationBreakdown
                            && league.EsAvg > 0 && league.PpAvg > 0
                            && home.EsAttackPerGame > 0 && away.EsAttackPerGame > 0;

        if (useBreakdown)
        {
            double homeEs = (home.EsAttackPerGame * away.EsDefensePerGame) / league.EsAvg;
            double awayEs = (away.EsAttackPerGame * home.EsDefensePerGame) / league.EsAvg;

            double homePp = (home.PpAttackPerGame * away.PkDefensePerGame) / league.PpAvg;
            double awayPp = (away.PpAttackPerGame * home.PkDefensePerGame) / league.PpAvg;

            homeExpGoals = homeEs + homePp + league.OtherAvg;
            awayExpGoals = awayEs + awayPp + league.OtherAvg;
        }
        else
        {
            // Fallback till multiplikativ "all"-modell
            double hAtk = home.AllAttackPerGame > 0 ? home.AllAttackPerGame : home.xGoalsFor / Math.Max(1, home.games_played);
            double aDef = away.AllDefensePerGame > 0 ? away.AllDefensePerGame : away.xGoalsAgainst / Math.Max(1, away.games_played);
            double aAtk = away.AllAttackPerGame > 0 ? away.AllAttackPerGame : away.xGoalsFor / Math.Max(1, away.games_played);
            double hDef = home.AllDefensePerGame > 0 ? home.AllDefensePerGame : home.xGoalsAgainst / Math.Max(1, home.games_played);

            homeExpGoals = (hAtk * aDef) / league.AllAvg;
            awayExpGoals = (aAtk * hDef) / league.AllAvg;
        }

        // NHL-hemmafördel (~4%)
        homeExpGoals *= 1.04;
        awayExpGoals *= 0.96;

        // Målvaktsfaktor påverkar MOTSTÅNDARENS förväntade mål
        homeExpGoals *= awayGoalieFactor;
        awayExpGoals *= homeGoalieFactor;

        // Back-to-Back (spelade igår: -5% eget anfall, +5% insläppta)
        if (homeBackToBack)
        {
            homeExpGoals *= 0.95;
            awayExpGoals *= 1.05;
        }
        if (awayBackToBack)
        {
            awayExpGoals *= 0.95;
            homeExpGoals *= 1.05;
        }

        homeExpGoals = Math.Max(0.4, homeExpGoals);
        awayExpGoals = Math.Max(0.4, awayExpGoals);

        var probs = new HockeyMatchProbabilities
        {
            ExpectedHomeGoals = homeExpGoals,
            ExpectedAwayGoals = awayExpGoals
        };

        // Simulera upp till 15 mål per lag
        for (int h = 0; h <= 15; h++)
        {
            for (int a = 0; a <= 15; a++)
            {
                double prob = Poisson(h, homeExpGoals) * Poisson(a, awayExpGoals);

                if (h > a)
                {
                    probs.HomeWinReg += prob;
                    if (h - a >= 2) probs.HomePucklineMinus15 += prob;
                }
                else if (a > h)
                {
                    probs.AwayWinReg += prob;
                    if (a - h >= 2) probs.AwayPucklineMinus15 += prob;
                }
                else
                {
                    probs.DrawReg += prob;
                }

                // Vid kryss efter 60 min tillkommer alltid +1 avgörande mål i OT/SO
                int totalGoalsInclOt = (h == a) ? (h + a + 1) : (h + a);

                if (totalGoalsInclOt > 5.5) probs.Over55 += prob;
                else probs.Under55 += prob;

                if (totalGoalsInclOt > 6.5) probs.Over65 += prob;
                else probs.Under65 += prob;
            }
        }

        // Normalisera så att sannolikheterna alltid summerar till exakt 100%
        double total = probs.HomeWinReg + probs.DrawReg + probs.AwayWinReg;
        if (total > 0)
        {
            probs.HomeWinReg /= total;
            probs.DrawReg /= total;
            probs.AwayWinReg /= total;
            probs.HomePucklineMinus15 /= total;
            probs.AwayPucklineMinus15 /= total;
            probs.Over55 /= total;
            probs.Under55 /= total;
            probs.Over65 /= total;
            probs.Under65 /= total;
        }

        // Lätt styrkeviktad OT/SO-fördelning istället för stel 50/50
        double homeStrengthShare = homeExpGoals / (homeExpGoals + awayExpGoals);
        double homeOtWinRate = Math.Clamp(0.50 + (homeStrengthShare - 0.50) * 0.35, 0.42, 0.58);

        probs.HomeMoneyline = probs.HomeWinReg + (probs.DrawReg * homeOtWinRate);
        probs.AwayMoneyline = probs.AwayWinReg + (probs.DrawReg * (1.0 - homeOtWinRate));

        return probs;
    }

    private double Poisson(int k, double lambda)
    {
        return (Math.Pow(lambda, k) * Math.Exp(-lambda)) / Factorial(k);
    }

    private double Factorial(int n)
    {
        if (n <= 1) return 1.0;
        double result = 1.0;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }
}
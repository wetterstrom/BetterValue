using BetterValue.Models;

namespace BetterValue.Service;

public interface ITeamStatisticsAggregator
{
    IReadOnlyList<Team> CalculateTeamStatistics(IReadOnlyList<FootballDataMatch> matches);
}

public class TeamStatisticsAggregator : ITeamStatisticsAggregator
{
    public IReadOnlyList<Team> CalculateTeamStatistics(IReadOnlyList<FootballDataMatch> matches)
    {
        if (matches == null || !matches.Any())
            return new List<Team>();

        // 1. Räkna ut ligans faktiska snitt för konvertering av skott på mål
        var matchesWithShots = matches.Where(m => m.HomeShotsOnTarget.HasValue && m.AwayShotsOnTarget.HasValue).ToList();
        double totalShots = matchesWithShots.Sum(m => m.HomeShotsOnTarget!.Value + m.AwayShotsOnTarget!.Value);
        double totalGoalsInShotMatches = matchesWithShots.Sum(m => m.HomeGoals + m.AwayGoals);
        double goalsPerShot = totalShots > 0 ? totalGoalsInShotMatches / totalShots : 0.30;

        // Hjälpfunktion: Blandar 50% mål och 50% skott på mål (om skottdata finns)
        double GetEffectiveGoals(int goals, int? shotsOnTarget)
        {
            if (!shotsOnTarget.HasValue) return goals;
            return (goals * 0.5) + (shotsOnTarget.Value * goalsPerShot * 0.5);
        }

        // 2. Beräkna ligans hemma- och bortasnitt (ger oss den faktiska hemmafördelen)
        double leagueHomeAvg = matches.Average(m => GetEffectiveGoals(m.HomeGoals, m.HomeShotsOnTarget));
        double leagueAwayAvg = matches.Average(m => GetEffectiveGoals(m.AwayGoals, m.AwayShotsOnTarget));

        // Hitta alla unika lag
        var teamNames = matches.Select(m => m.HomeTeam)
                               .Concat(matches.Select(m => m.AwayTeam))
                               .Distinct()
                               .ToList();

        // Startvärden: Alla lag börjar som exakt medelbra (1.0)
        var attack = teamNames.ToDictionary(t => t, _ => 1.0);
        var defense = teamNames.ToDictionary(t => t, _ => 1.0);

        // 3. Iterativ motståndarjustering (15 rundor räcker för att värdena ska stabiliseras)
        for (int iter = 0; iter < 15; iter++)
        {
            var newAttack = new Dictionary<string, double>();
            var newDefense = new Dictionary<string, double>();

            foreach (var team in teamNames)
            {
                double scoredActual = 0;
                double scoredExpected = 0;
                double concededActual = 0;
                double concededExpected = 0;

                foreach (var m in matches)
                {
                    if (m.HomeTeam == team)
                    {
                        scoredActual += GetEffectiveGoals(m.HomeGoals, m.HomeShotsOnTarget);
                        scoredExpected += leagueHomeAvg * defense[m.AwayTeam];

                        concededActual += GetEffectiveGoals(m.AwayGoals, m.AwayShotsOnTarget);
                        concededExpected += leagueAwayAvg * attack[m.AwayTeam];
                    }
                    else if (m.AwayTeam == team)
                    {
                        scoredActual += GetEffectiveGoals(m.AwayGoals, m.AwayShotsOnTarget);
                        scoredExpected += leagueAwayAvg * defense[m.HomeTeam];

                        concededActual += GetEffectiveGoals(m.HomeGoals, m.HomeShotsOnTarget);
                        concededExpected += leagueHomeAvg * attack[m.HomeTeam];
                    }
                }

                newAttack[team] = scoredExpected > 0 ? scoredActual / scoredExpected : 1.0;
                newDefense[team] = concededExpected > 0 ? concededActual / concededExpected : 1.0;
            }

            // Normalisera så att ligans snitt alltid förblir exakt 1.0 (hindrar värdena från att glida iväg)
            double avgAtk = newAttack.Values.Average();
            double avgDef = newDefense.Values.Average();

            foreach (var team in teamNames)
            {
                attack[team] = newAttack[team] / avgAtk;
                defense[team] = newDefense[team] / avgDef;
            }
        }

        return teamNames.Select(name => new Team(
        name,
        attack[name] * leagueHomeAvg,
        defense[name] * leagueAwayAvg,
        attack[name] * leagueAwayAvg,
        defense[name] * leagueHomeAvg,
        matches.Count(m => m.HomeTeam == name),
        matches.Count(m => m.AwayTeam == name)
    )).ToList();
    }

    // Intern hjälpklass för att hålla datan
    private class TeamMatchData
    {
        public bool IsHome { get; set; }
        public int Scored { get; set; }
        public int Conceded { get; set; }
    }
}
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
        // 1. Samla alla matcher per lag i kronologisk ordning
        var teamMatches = new Dictionary<string, List<TeamMatchData>>();

        foreach (var match in matches)
        {
            if (!teamMatches.ContainsKey(match.HomeTeam)) teamMatches[match.HomeTeam] = new List<TeamMatchData>();
            if (!teamMatches.ContainsKey(match.AwayTeam)) teamMatches[match.AwayTeam] = new List<TeamMatchData>();

            teamMatches[match.HomeTeam].Add(new TeamMatchData { IsHome = true, Scored = match.HomeGoals, Conceded = match.AwayGoals });
            teamMatches[match.AwayTeam].Add(new TeamMatchData { IsHome = false, Scored = match.AwayGoals, Conceded = match.HomeGoals });
        }

        var result = new List<Team>();

        // ==========================================
        // ⚙️ KONFIGURATION FÖR VIKTNING (TIDSFAKTOR)
        // ==========================================
        int recentMatchesCount = 5;  // Antal matcher som ska räknas som "Nylig form"
        double recentWeight = 2.5;   // De 5 senaste matcherna väger 2.5x tyngre
        double normalWeight = 1.0;   // Resterande matcher i början av säsongen

        foreach (var kvp in teamMatches)
        {
            string teamName = kvp.Key;
            var allMatches = kvp.Value;
            int totalMatches = allMatches.Count;

            double homeScoredWeighted = 0, homeConcededWeighted = 0, homeWeightSum = 0;
            double awayScoredWeighted = 0, awayConcededWeighted = 0, awayWeightSum = 0;

            int homeCount = 0, awayCount = 0;

            // 2. Loopa över lagets matcher från äldst till nyast
            for (int i = 0; i < totalMatches; i++)
            {
                var m = allMatches[i];

                // Om matchen är inom de sista 'recentMatchesCount' i listan
                bool isRecent = i >= (totalMatches - recentMatchesCount);
                double weight = isRecent ? recentWeight : normalWeight;

                if (m.IsHome)
                {
                    homeScoredWeighted += m.Scored * weight;
                    homeConcededWeighted += m.Conceded * weight;
                    homeWeightSum += weight;
                    homeCount++;
                }
                else
                {
                    awayScoredWeighted += m.Scored * weight;
                    awayConcededWeighted += m.Conceded * weight;
                    awayWeightSum += weight;
                    awayCount++;
                }
            }

            // 3. Räkna ut det viktade snittet
            result.Add(new Team(
                Name: teamName,
                HomeGoalsScoredAvg: homeWeightSum > 0 ? homeScoredWeighted / homeWeightSum : 0,
                HomeGoalsConcededAvg: homeWeightSum > 0 ? homeConcededWeighted / homeWeightSum : 0,
                AwayGoalsScoredAvg: awayWeightSum > 0 ? awayScoredWeighted / awayWeightSum : 0,
                AwayGoalsConcededAvg: awayWeightSum > 0 ? awayConcededWeighted / awayWeightSum : 0,
                MatchesPlayedHome: homeCount,
                MatchesPlayedAway: awayCount
            ));
        }

        return result;
    }

    // Intern hjälpklass för att hålla datan
    private class TeamMatchData
    {
        public bool IsHome { get; set; }
        public int Scored { get; set; }
        public int Conceded { get; set; }
    }
}
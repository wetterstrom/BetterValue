namespace BetterValue.Models;

public record Team(
    string Name,
    double HomeGoalsScoredAvg,
    double HomeGoalsConcededAvg,
    double AwayGoalsScoredAvg,
    double AwayGoalsConcededAvg,
    int MatchesPlayedHome,
    int MatchesPlayedAway
);
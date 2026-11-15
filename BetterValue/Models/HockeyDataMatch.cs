namespace BetterValue.Models;

public class HockeyDataMatch
{
    // Namnen matchar exakt kolumnerna i din teams.csv-fil
    public string team { get; set; } = string.Empty;
    public int season { get; set; }
    public string situation { get; set; } = string.Empty;
    public int games_played { get; set; }
    public double goalsFor { get; set; }
    public double goalsAgainst { get; set; }
    public double xGoalsFor { get; set; }
    public double xGoalsAgainst { get; set; }
}
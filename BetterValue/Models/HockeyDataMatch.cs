namespace BetterValue.Models;

public class HockeyDataMatch
{
    // Befintliga fält från "all"-raden
    public string team { get; set; } = string.Empty;
    public int season { get; set; }
    public string situation { get; set; } = string.Empty;
    public int games_played { get; set; }
    public double goalsFor { get; set; }
    public double goalsAgainst { get; set; }
    public double xGoalsFor { get; set; }
    public double xGoalsAgainst { get; set; }

    // Viktade styrketal per match (65% justerad xG + 35% faktiska mål)
    public double AllAttackPerGame { get; set; }
    public double AllDefensePerGame { get; set; }

    // Special Teams & 5v5 från MoneyPuck-raderna (5on5, 5on4, 4on5)
    public double EsAttackPerGame { get; set; }
    public double EsDefensePerGame { get; set; }
    public double PpAttackPerGame { get; set; }
    public double PkDefensePerGame { get; set; }
    public bool HasSituationBreakdown { get; set; }
}
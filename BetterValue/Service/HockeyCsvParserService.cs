using BetterValue.Models;
using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;

namespace BetterValue.Service;

public interface IHockeyCsvParserService
{
    Task<IEnumerable<HockeyDataMatch>> ParseTeamsAsync(IBrowserFile file);
}

public class HockeyCsvParserService : IHockeyCsvParserService
{
    public async Task<IEnumerable<HockeyDataMatch>> ParseTeamsAsync(IBrowserFile file)
    {
        var teamsByName = new Dictionary<string, HockeyDataMatch>(StringComparer.OrdinalIgnoreCase);

        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var reader = new StreamReader(stream);

        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine)) return teamsByName.Values;

        var headers = headerLine.Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToList();

        int idxTeam = headers.IndexOf("team");
        int idxSeason = headers.IndexOf("season");
        int idxSituation = headers.IndexOf("situation");
        int idxGames = headers.IndexOf("games_played");
        int idxGoalsFor = headers.IndexOf("goalsfor");
        int idxGoalsAgainst = headers.IndexOf("goalsagainst");
        int idxXGoalsFor = headers.IndexOf("xgoalsfor");
        int idxXGoalsAgainst = headers.IndexOf("xgoalsagainst");

        // MoneyPucks justerade xG-kolumner (rensar score effects och returkluster)
        int idxAdjXgf = headers.IndexOf("flurryscorevenueadjustedxgoalsfor");
        int idxAdjXga = headers.IndexOf("flurryscorevenueadjustedxgoalsagainst");

        if (idxTeam < 0 || idxSituation < 0 || idxGames < 0 || idxGoalsFor < 0 || idxGoalsAgainst < 0)
            return teamsByName.Values;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            string teamName = GetString(cols, idxTeam);
            string situation = GetString(cols, idxSituation).ToLowerInvariant();

            if (string.IsNullOrEmpty(teamName)) continue;
            if (situation is not ("all" or "5on5" or "5on4" or "4on5")) continue;

            int games = Math.Max(1, GetInt(cols, idxGames));
            double gf = GetDouble(cols, idxGoalsFor);
            double ga = GetDouble(cols, idxGoalsAgainst);
            double xgf = GetDouble(cols, idxXGoalsFor);
            double xga = GetDouble(cols, idxXGoalsAgainst);

            // Använd justerad xG om den finns, annars vanlig xG
            double effXgf = idxAdjXgf >= 0 ? GetDouble(cols, idxAdjXgf, xgf) : xgf;
            double effXga = idxAdjXga >= 0 ? GetDouble(cols, idxAdjXga, xga) : xga;

            // 65% xG + 35% faktiska mål per match
            double attackRate = ((effXgf * 0.65) + (gf * 0.35)) / games;
            double defenseRate = ((effXga * 0.65) + (ga * 0.35)) / games;

            if (!teamsByName.TryGetValue(teamName, out var team))
            {
                team = new HockeyDataMatch
                {
                    team = teamName,
                    season = GetInt(cols, idxSeason),
                    situation = "all"
                };
                teamsByName[teamName] = team;
            }

            switch (situation)
            {
                case "all":
                    team.games_played = games;
                    team.goalsFor = gf;
                    team.goalsAgainst = ga;
                    team.xGoalsFor = xgf;
                    team.xGoalsAgainst = xga;
                    team.AllAttackPerGame = attackRate;
                    team.AllDefensePerGame = defenseRate;
                    break;

                case "5on5":
                    team.EsAttackPerGame = attackRate;
                    team.EsDefensePerGame = defenseRate;
                    team.HasSituationBreakdown = true;
                    break;

                case "5on4":
                    team.PpAttackPerGame = attackRate;
                    break;

                case "4on5":
                    team.PkDefensePerGame = defenseRate;
                    break;
            }
        }

        return teamsByName.Values.Where(t => t.games_played > 0).ToList();
    }

    private static string GetString(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return "";
        return cols[idx].Trim().Trim('"');
    }

    private static int GetInt(string[] cols, int idx)
    {
        var raw = GetString(cols, idx);
        return int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out int val) ? val : 0;
    }

    private static double GetDouble(string[] cols, int idx, double fallback = 0)
    {
        var raw = GetString(cols, idx);
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) ? val : fallback;
    }
}
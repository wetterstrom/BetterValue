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
        var list = new List<HockeyDataMatch>();

        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var reader = new StreamReader(stream);

        // 1. Läs rubrikerna för att dynamiskt hitta rätt kolumner
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine)) return list;

        var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();

        int idxTeam = headers.IndexOf("team");
        int idxSeason = headers.IndexOf("season");
        int idxSituation = headers.IndexOf("situation");
        int idxGames = headers.IndexOf("games_played");
        int idxGoalsFor = headers.IndexOf("goalsfor");
        int idxGoalsAgainst = headers.IndexOf("goalsagainst");
        int idxXGoalsFor = headers.IndexOf("xgoalsfor");
        int idxXGoalsAgainst = headers.IndexOf("xgoalsagainst");

        // 2. Loopa asynkront tills ReadLineAsync returnerar null (slutet av filen)
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length <= Math.Max(idxXGoalsAgainst, idxTeam)) continue;

            string situation = cols[idxSituation].Trim();

            if (situation == "all")
            {
                var match = new HockeyDataMatch
                {
                    team = cols[idxTeam].Trim(),
                    season = int.Parse(cols[idxSeason].Trim(), CultureInfo.InvariantCulture),
                    situation = situation,
                    games_played = int.Parse(cols[idxGames].Trim(), CultureInfo.InvariantCulture),
                    goalsFor = double.Parse(cols[idxGoalsFor].Trim(), CultureInfo.InvariantCulture),
                    goalsAgainst = double.Parse(cols[idxGoalsAgainst].Trim(), CultureInfo.InvariantCulture),
                    xGoalsFor = double.Parse(cols[idxXGoalsFor].Trim(), CultureInfo.InvariantCulture),
                    xGoalsAgainst = double.Parse(cols[idxXGoalsAgainst].Trim(), CultureInfo.InvariantCulture)
                };
                list.Add(match);
            }
        }

        return list;
    }
}
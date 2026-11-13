using CsvHelper;
using Microsoft.AspNetCore.Components.Forms;
using BetterValue.Models;

namespace BetterValue.Service;

public interface ICsvParserService
{
    Task<IReadOnlyList<FootballDataMatch>> ParseMatchesAsync(IBrowserFile file);
}

public class CsvParserService : ICsvParserService
{
    public async Task<IReadOnlyList<FootballDataMatch>> ParseMatchesAsync(IBrowserFile file)
    {
        var matches = new List<FootballDataMatch>();

        using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);

        // I version 34 används Create() direkt på filströmmen
        await using var csv = CsvReader.Create(stream);

        await foreach (var record in csv.GetRecordsAsync<FootballDataMatch>())
        {
            matches.Add(record);
        }

        return matches;
    }
}
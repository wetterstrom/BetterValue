using ExcelDataReader;
using System.Data;
using System.Globalization;
using BetterValue.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace BetterValue.Service;

public interface IExcelParserService
{
    Task<IReadOnlyList<FootballDataMatch>> ParseMatchesAsync(IBrowserFile file);
}

public class ExcelParserService : IExcelParserService
{
    public async Task<IReadOnlyList<FootballDataMatch>> ParseMatchesAsync(IBrowserFile file)
    {
        var matches = new List<FootballDataMatch>();

        // Läs in filen till arbetsminnet
        using var stream = new MemoryStream();
        await file.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024).CopyToAsync(stream);
        stream.Position = 0;

        // Skapa läsaren
        using var reader = ExcelReaderFactory.CreateReader(stream);

        // Konvertera hela Excel-filen (alla flikar) till ett DataSet
        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
            {
                UseHeaderRow = true // Säger till att första raden är rubriker
            }
        });

        // Loopa igenom varje flik (Sheet) i Excel-filen
        foreach (DataTable table in result.Tables)
        {
            foreach (DataRow row in table.Rows)
            {
                try
                {
                    // Hjälpfunktioner för att läsa kolumner säkert
                    string GetString(string col) => table.Columns.Contains(col) ? row[col]?.ToString() ?? "" : "";
                    int GetInt(string col) => table.Columns.Contains(col) && int.TryParse(row[col]?.ToString(), out int val) ? val : 0;

                    int? GetNullableInt(string col)
                    {
                        if (!table.Columns.Contains(col) || row[col] == null || row[col] == DBNull.Value)
                            return null;

                        var raw = row[col].ToString()?.Trim().Replace(",", ".");
                        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                            return (int)Math.Round(val);

                        return null;
                    }

                    // Hanterar både punkt och komma för decimaler i odds
                    double? GetDouble(string col) => table.Columns.Contains(col) && double.TryParse(row[col]?.ToString()?.Replace(",", "."), out double val) ? val : null;

                    var homeTeam = GetString("HomeTeam");
                    var awayTeam = GetString("AwayTeam");

                    // Hoppa över tomma rader
                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam)) continue;

                    // Hämta ligan från kolumnen 'Div', eller använd flikens namn om kolumnen saknas
                    var div = GetString("Div");
                    if (string.IsNullOrEmpty(div)) div = table.TableName;

                    matches.Add(new FootballDataMatch(
                        Division: div,
                        DateString: GetString("Date"),
                        HomeTeam: homeTeam,
                        AwayTeam: awayTeam,
                        HomeGoals: GetInt("FTHG"),
                        AwayGoals: GetInt("FTAG"),
                        OddsHome: GetDouble("B365H"),
                        OddsDraw: GetDouble("B365D"),
                        OddsAway: GetDouble("B365A"),
                        HomeShotsOnTarget: GetNullableInt("HST"),
                        AwayShotsOnTarget: GetNullableInt("AST")
                    ));
                }
                catch
                {
                    // Ignorera rader som är korrupta och fortsätt skanna
                }
            }
        }

        return matches;
    }
}
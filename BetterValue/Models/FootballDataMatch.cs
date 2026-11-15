using CsvHelper;

namespace BetterValue.Models;

[CsvRecord]
public partial record FootballDataMatch(
    [CsvColumn("Div")] string Division,
    [CsvColumn("Date")] string DateString,
    [CsvColumn("HomeTeam")] string HomeTeam,
    [CsvColumn("AwayTeam")] string AwayTeam,
    [CsvColumn("FTHG")] int HomeGoals,
    [CsvColumn("FTAG")] int AwayGoals,
    [CsvColumn("B365H")] double? OddsHome,
    [CsvColumn("B365D")] double? OddsDraw,
    [CsvColumn("B365A")] double? OddsAway
);
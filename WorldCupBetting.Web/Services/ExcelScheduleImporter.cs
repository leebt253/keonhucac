using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Services;

public class ExcelScheduleImporter(AppDbContext db)
{
    private sealed class ImportedMatchRow
    {
        public int MatchNo { get; init; }
        public DateTime MatchTime { get; init; }
        public string TeamA { get; init; } = string.Empty;
        public string TeamB { get; init; } = string.Empty;
        public string GroupCode { get; init; } = string.Empty;
        public TournamentRound Round { get; init; } = TournamentRound.Group;
    }

    private static readonly Dictionary<string, string> TeamNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["South Korea"] = "Hàn Quốc",
        ["Czech Republic"] = "CH Séc",
        ["USA"] = "Mỹ",
        ["Germany"] = "Đức",
        ["Japan"] = "Nhật Bản",
        ["Netherlands"] = "Hà Lan",
        ["Sweden"] = "Thụy Điển",
        ["Switzerland"] = "Thụy Sĩ",
        ["Spain"] = "Tây Ban Nha",
        ["Portugal"] = "Bồ Đào Nha",
        ["Austria"] = "Áo",
        ["Saudi Arabia"] = "Ả Rập Xê Út",
        ["Turkey"] = "Thổ Nhĩ Kỳ",
        ["Norway"] = "Na Uy",
        ["France"] = "Pháp",
        ["England"] = "Anh"
    };

    public async Task<int> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var wbPart = doc.WorkbookPart;
        if (wbPart is null)
        {
            return 0;
        }

        var shared = wbPart.SharedStringTablePart?.SharedStringTable;

        if (IsSimpleScheduleWorkbook(wbPart))
        {
            return await ImportSimpleScheduleAsync(wbPart, shared);
        }

        var teamGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var importedMatches = new List<(DateTime MatchTime, string TeamA, string TeamB, string GroupCode, int MatchNo)>();

        var dailySheet = FindSheet(wbPart, "DailySchedule");
        if (dailySheet is not null)
        {
            var wsPart = (WorksheetPart)wbPart.GetPartById(dailySheet.Id!);
            var rows = wsPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? Enumerable.Empty<Row>();

            foreach (var row in rows.Where(r => r.RowIndex is not null && r.RowIndex >= 4))
            {
                var teamA = NormalizeTeamName(ReadCellText(wsPart, shared, row.RowIndex!.Value, 4).Trim());
                var teamB = NormalizeTeamName(ReadCellText(wsPart, shared, row.RowIndex!.Value, 5).Trim());
                if (string.IsNullOrWhiteSpace(teamA) || string.IsNullOrWhiteSpace(teamB))
                {
                    continue;
                }

                var matchNoText = ReadCellText(wsPart, shared, row.RowIndex!.Value, 6).Trim();
                var codeA = ReadCellText(wsPart, shared, row.RowIndex!.Value, 7).Trim();
                var codeB = ReadCellText(wsPart, shared, row.RowIndex!.Value, 8).Trim();
                var dateText = ReadCellText(wsPart, shared, row.RowIndex!.Value, 2).Trim();
                var timeText = ReadCellText(wsPart, shared, row.RowIndex!.Value, 3).Trim();

                var groupCode = ExtractGroupCode(codeA);
                if (string.IsNullOrWhiteSpace(groupCode))
                {
                    groupCode = ExtractGroupCode(codeB);
                }

                if (!string.IsNullOrWhiteSpace(groupCode))
                {
                    teamGroup[teamA] = groupCode;
                    teamGroup[teamB] = groupCode;
                }

                var matchTime = ParseExcelDateTime(timeText);
                if (matchTime == default)
                {
                    matchTime = ParseExcelDateTime(dateText);
                }
                if (matchTime == default)
                {
                    continue;
                }

                var matchNo = int.TryParse(matchNoText.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), out var n) ? n : 0;
                importedMatches.Add((matchTime, teamA, teamB, groupCode, matchNo));
            }
        }

        var worldCupSheet = FindSheet(wbPart, "World Cup");
        if (worldCupSheet is not null)
        {
            var wsPart = (WorksheetPart)wbPart.GetPartById(worldCupSheet.Id!);
            var groupLetter = 'A';
            for (var col = 2; col <= 80; col += 3)
            {
                var foundAnyTeam = false;
                for (uint row = 4; row <= 7; row++)
                {
                    var team = NormalizeTeamName(ReadCellText(wsPart, shared, row, col).Trim());
                    if (string.IsNullOrWhiteSpace(team))
                    {
                        continue;
                    }

                    foundAnyTeam = true;
                    var g = groupLetter.ToString();
                    if (!teamGroup.ContainsKey(team))
                    {
                        teamGroup[team] = g;
                    }
                }

                if (foundAnyTeam)
                {
                    groupLetter++;
                }
            }
        }

        var teams = await db.Teams.ToListAsync();
        foreach (var t in teams)
        {
            t.Name = NormalizeTeamName(t.Name);
        }

        foreach (var pair in teamGroup)
        {
            var existing = teams.FirstOrDefault(t => string.Equals(t.Name, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new Team { Name = pair.Key, GroupCode = pair.Value };
                db.Teams.Add(existing);
                teams.Add(existing);
            }
            else if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                existing.GroupCode = pair.Value;
            }
        }

        var matches = await db.Matches.Where(x => x.Round == TournamentRound.Group).ToListAsync();
        foreach (var m in await db.Matches.ToListAsync())
        {
            m.TeamA = NormalizeTeamName(m.TeamA);
            m.TeamB = NormalizeTeamName(m.TeamB);
            m.FavoriteTeam = NormalizeTeamName(m.FavoriteTeam);
        }

        foreach (var row in importedMatches)
        {
            var existing = matches.FirstOrDefault(m =>
                (string.Equals(m.TeamA, row.TeamA, StringComparison.OrdinalIgnoreCase) && string.Equals(m.TeamB, row.TeamB, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(m.TeamA, row.TeamB, StringComparison.OrdinalIgnoreCase) && string.Equals(m.TeamB, row.TeamA, StringComparison.OrdinalIgnoreCase)));

            if (existing is null)
            {
                existing = new Match
                {
                    MatchTime = row.MatchTime,
                    TeamA = row.TeamA,
                    TeamB = row.TeamB,
                    FavoriteTeam = row.TeamA,
                    HandicapValue = 0,
                    GroupCode = row.GroupCode,
                    Round = TournamentRound.Group,
                    Status = MatchStatus.Upcoming
                };
                db.Matches.Add(existing);
                matches.Add(existing);
            }
            else
            {
                existing.MatchTime = row.MatchTime;
                if (!string.IsNullOrWhiteSpace(row.GroupCode))
                {
                    existing.GroupCode = row.GroupCode;
                }
            }
        }

        await EnsureKnockoutPlaceholdersAsync(importedMatches.Select(x => x.MatchTime).DefaultIfEmpty(DateTime.Now).Max());

        await db.SaveChangesAsync();
        return importedMatches.Count;
    }

    private async Task<int> ImportSimpleScheduleAsync(WorkbookPart wbPart, SharedStringTable? shared)
    {
        var firstSheet = wbPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault();
        if (firstSheet is null)
        {
            return 0;
        }

        var wsPart = (WorksheetPart)wbPart.GetPartById(firstSheet.Id!);
        var rows = wsPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? Enumerable.Empty<Row>();

        var existingTeams = await db.Teams
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupCode))
            .ToListAsync();

        var teamGroup = existingTeams
            .GroupBy(x => NormalizeTeamName(x.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Select(t => t.GroupCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var importedMatches = new List<ImportedMatchRow>();
        var currentRound = TournamentRound.Group;

        foreach (var row in rows)
        {
            if (row.RowIndex is null)
            {
                continue;
            }

            var idText = ReadCellText(wsPart, shared, row.RowIndex.Value, 1).Trim();
            var col2Text = ReadCellText(wsPart, shared, row.RowIndex.Value, 2).Trim();
            var timeText = ReadCellText(wsPart, shared, row.RowIndex.Value, 3).Trim();
            var teamA = NormalizeTeamName(ReadCellText(wsPart, shared, row.RowIndex.Value, 4).Trim());
            var teamB = NormalizeTeamName(ReadCellText(wsPart, shared, row.RowIndex.Value, 5).Trim());

            if (!string.IsNullOrWhiteSpace(col2Text) && TryParseRoundHeader(col2Text, out var parsedRound))
            {
                currentRound = parsedRound;
                continue;
            }

            if (!int.TryParse(idText, out var matchNo))
            {
                continue;
            }

            var matchTime = ParseScheduleDateTime(col2Text, timeText);
            if (matchTime == default)
            {
                continue;
            }

            var round = matchNo <= 72 ? TournamentRound.Group : currentRound;
            var groupCode = round == TournamentRound.Group
                ? ResolveGroupCode(teamA, teamB, teamGroup, matchNo)
                : "KO";

            if (round == TournamentRound.Group)
            {
                if (!string.IsNullOrWhiteSpace(teamA) && !string.IsNullOrWhiteSpace(groupCode))
                {
                    teamGroup[teamA] = groupCode;
                }

                if (!string.IsNullOrWhiteSpace(teamB) && !string.IsNullOrWhiteSpace(groupCode))
                {
                    teamGroup[teamB] = groupCode;
                }
            }

            importedMatches.Add(new ImportedMatchRow
            {
                MatchNo = matchNo,
                MatchTime = matchTime,
                TeamA = teamA,
                TeamB = teamB,
                GroupCode = groupCode,
                Round = round
            });
        }

        await SyncMatchesByIdAsync(importedMatches);
        return importedMatches.Count;
    }

    private async Task SyncMatchesByIdAsync(List<ImportedMatchRow> importedMatches)
    {
        var existingMatches = await db.Matches.OrderBy(x => x.Id).ToListAsync();
        var existingById = existingMatches.ToDictionary(x => x.Id);
        var importedIds = importedMatches.Select(x => x.MatchNo).ToHashSet();

        foreach (var row in importedMatches.OrderBy(x => x.MatchNo))
        {
            if (!existingById.TryGetValue(row.MatchNo, out var match))
            {
                match = new Match { Id = row.MatchNo };
                db.Matches.Add(match);
                existingById[row.MatchNo] = match;
            }

            match.MatchTime = row.MatchTime;
            match.TeamA = row.TeamA;
            match.TeamB = row.TeamB;
            match.Round = row.Round;
            match.GroupCode = row.GroupCode;
            match.Status = MatchStatus.Upcoming;

            if (row.Round == TournamentRound.Group)
            {
                if (!string.Equals(match.FavoriteTeam, row.TeamA, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.FavoriteTeam, row.TeamB, StringComparison.OrdinalIgnoreCase))
                {
                    match.FavoriteTeam = row.TeamA;
                }

                if (!string.IsNullOrWhiteSpace(match.Result) &&
                    !string.Equals(match.Result, row.TeamA, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.Result, row.TeamB, StringComparison.OrdinalIgnoreCase))
                {
                    match.Result = string.Empty;
                }
            }
            else
            {
                if (!string.Equals(match.FavoriteTeam, row.TeamA, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.FavoriteTeam, row.TeamB, StringComparison.OrdinalIgnoreCase))
                {
                    match.FavoriteTeam = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(match.Result) &&
                    !string.Equals(match.Result, row.TeamA, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(match.Result, row.TeamB, StringComparison.OrdinalIgnoreCase))
                {
                    match.Result = string.Empty;
                }
            }
        }

        var extraMatches = existingMatches.Where(x => !importedIds.Contains(x.Id)).ToList();
        if (extraMatches.Count > 0)
        {
            var extraIds = extraMatches.Select(x => x.Id).ToList();

            var extraPredictions = await db.Predictions.Where(x => extraIds.Contains(x.MatchId)).ToListAsync();
            if (extraPredictions.Count > 0)
            {
                db.Predictions.RemoveRange(extraPredictions);
            }

            var extraBetResults = await db.BetResults.Where(x => extraIds.Contains(x.MatchId)).ToListAsync();
            if (extraBetResults.Count > 0)
            {
                db.BetResults.RemoveRange(extraBetResults);
            }

            foreach (var match in await db.Matches.Where(x => x.ParentMatchAId.HasValue || x.ParentMatchBId.HasValue).ToListAsync())
            {
                if (match.ParentMatchAId.HasValue && extraIds.Contains(match.ParentMatchAId.Value))
                {
                    match.ParentMatchAId = null;
                }

                if (match.ParentMatchBId.HasValue && extraIds.Contains(match.ParentMatchBId.Value))
                {
                    match.ParentMatchBId = null;
                }
            }

            db.Matches.RemoveRange(extraMatches);
        }

        await db.SaveChangesAsync();
    }

    private static bool IsSimpleScheduleWorkbook(WorkbookPart wbPart)
    {
        var sheets = wbPart.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
        if (sheets.Count != 1)
        {
            return false;
        }

        return !string.Equals(sheets[0].Name?.Value, "DailySchedule", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRoundHeader(string value, out TournamentRound round)
    {
        var normalized = value.Trim().ToLowerInvariant();
        round = TournamentRound.Group;

        if (normalized.Contains("round of 32"))
        {
            round = TournamentRound.RoundOf32;
            return true;
        }

        if (normalized.Contains("round of 16"))
        {
            round = TournamentRound.RoundOf16;
            return true;
        }

        if (normalized.Contains("quarter final"))
        {
            round = TournamentRound.QuarterFinal;
            return true;
        }

        if (normalized.Contains("semi-final") || normalized.Contains("semifinal"))
        {
            round = TournamentRound.SemiFinal;
            return true;
        }

        if (normalized.Contains("hạng 3"))
        {
            round = TournamentRound.ThirdPlace;
            return true;
        }

        if (normalized.Contains("chung kết"))
        {
            round = TournamentRound.Final;
            return true;
        }

        return false;
    }

    private static string ResolveGroupCode(string teamA, string teamB, Dictionary<string, string> teamGroup, int matchNo)
    {
        if (!string.IsNullOrWhiteSpace(teamA) && teamGroup.TryGetValue(teamA, out var groupA))
        {
            return groupA;
        }

        if (!string.IsNullOrWhiteSpace(teamB) && teamGroup.TryGetValue(teamB, out var groupB))
        {
            return groupB;
        }

        var groupIndex = (matchNo - 1) / 6;
        if (groupIndex >= 0 && groupIndex < 12)
        {
            return ((char)('A' + groupIndex)).ToString();
        }

        return string.Empty;
    }

    private static DateTime ParseScheduleDateTime(string dateValue, string timeValue)
    {
        var combined = $"{dateValue} {timeValue}".Trim();
        var formats = new[]
        {
            "ddd, dd/MM/yyyy H:mm",
            "ddd, dd/MM/yyyy HH:mm",
            "dd/MM/yyyy H:mm",
            "dd/MM/yyyy HH:mm"
        };

        if (DateTime.TryParseExact(combined, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            return RoundToMinute(exact);
        }

        var datePart = ParseExcelDateTime(dateValue);
        if (datePart != default)
        {
            if (TimeOnly.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timeOnly) ||
                TimeOnly.TryParse(timeValue, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.AllowWhiteSpaces, out timeOnly))
            {
                return RoundToMinute(datePart.Date.Add(timeOnly.ToTimeSpan()));
            }

            var timePart = ParseExcelDateTime(timeValue);
            if (timePart != default)
            {
                return RoundToMinute(datePart.Date.Add(timePart.TimeOfDay));
            }
        }

        return default;
    }

    private static Sheet? FindSheet(WorkbookPart wbPart, string name)
    {
        return wbPart.Workbook.Sheets?
            .Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadCellText(WorksheetPart wsPart, SharedStringTable? shared, uint row, int col)
    {
        var cellRef = ColName(col) + row;
        var cell = wsPart.Worksheet.Descendants<Cell>()
            .FirstOrDefault(c => string.Equals(c.CellReference?.Value, cellRef, StringComparison.OrdinalIgnoreCase));

        if (cell is null)
        {
            return string.Empty;
        }

        var text = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString && shared is not null && int.TryParse(text, out var idx))
        {
            return shared.ElementAt(idx).InnerText;
        }

        return text;
    }

    private static string ColName(int index)
    {
        var name = string.Empty;
        var n = index;
        while (n > 0)
        {
            var rem = (n - 1) % 26;
            name = (char)('A' + rem) + name;
            n = (n - 1) / 26;
        }
        return name;
    }

    private static DateTime ParseExcelDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
        {
            try
            {
                return RoundToMinute(DateTime.FromOADate(oa));
            }
            catch
            {
                return default;
            }
        }

        if (DateTime.TryParse(value, out var dt))
        {
            return RoundToMinute(dt);
        }

        return default;
    }

    private static string ExtractGroupCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var c = code.Trim().FirstOrDefault(char.IsLetter);
        return c == default ? string.Empty : char.ToUpperInvariant(c).ToString();
    }

    private async Task EnsureKnockoutPlaceholdersAsync(DateTime lastGroupMatch)
    {
        if (await db.Matches.AnyAsync(x => x.Round != TournamentRound.Group))
        {
            return;
        }

        var start = lastGroupMatch.Date.AddDays(2).AddHours(19);
        var placeholders = new List<Match>();

        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.RoundOf32, 16, start));
        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.RoundOf16, 8, start.AddDays(6)));
        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.QuarterFinal, 4, start.AddDays(10)));
        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.SemiFinal, 2, start.AddDays(13)));
        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.ThirdPlace, 1, start.AddDays(17)));
        placeholders.AddRange(CreateRoundPlaceholders(TournamentRound.Final, 1, start.AddDays(18)));

        await db.Matches.AddRangeAsync(placeholders);
    }

    private static IEnumerable<Match> CreateRoundPlaceholders(TournamentRound round, int count, DateTime start)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new Match
            {
                MatchTime = start.AddHours(i * 3),
                TeamA = string.Empty,
                TeamB = string.Empty,
                FavoriteTeam = string.Empty,
                HandicapValue = 0,
                Result = string.Empty,
                Status = MatchStatus.Upcoming,
                Round = round,
                GroupCode = "KO"
            };
        }
    }

    private static DateTime RoundToMinute(DateTime dt)
    {
        var ticks = (long)Math.Round(dt.Ticks / (double)TimeSpan.TicksPerMinute) * TimeSpan.TicksPerMinute;
        return new DateTime(ticks, dt.Kind);
    }

    private static string NormalizeTeamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var value = name.Trim();

        try
        {
            if (value.Contains('├') || value.Contains('ß') || value.Contains('╣'))
            {
                var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
                value = Encoding.UTF8.GetString(bytes);
            }
        }
        catch
        {
            // Keep original text when encoding fallback fails.
        }

        value = value.Replace("/Herzeg.", "/Herzegovina", StringComparison.OrdinalIgnoreCase);

        if (TeamNameMap.TryGetValue(value, out var mapped))
        {
            return mapped;
        }

        return value;
    }
}

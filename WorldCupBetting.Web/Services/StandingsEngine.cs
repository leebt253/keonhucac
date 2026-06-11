using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Services;

public class StandingsEngine(AppDbContext db)
{
    public async Task RecalculateAsync()
    {
        db.GroupStandings.RemoveRange(db.GroupStandings);

        var groupMatches = await db.Matches
            .Where(x => x.Round == TournamentRound.Group && !string.IsNullOrWhiteSpace(x.Result))
            .ToListAsync();

        var map = new Dictionary<string, GroupStanding>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in groupMatches)
        {
            if (!ResultParser.TryParseScore(m.Result, out var a, out var b))
            {
                continue;
            }

            var keyA = $"{m.GroupCode}|{m.TeamA}";
            var keyB = $"{m.GroupCode}|{m.TeamB}";

            if (!map.TryGetValue(keyA, out var rowA))
            {
                rowA = new GroupStanding { GroupCode = m.GroupCode, TeamName = m.TeamA };
                map[keyA] = rowA;
            }

            if (!map.TryGetValue(keyB, out var rowB))
            {
                rowB = new GroupStanding { GroupCode = m.GroupCode, TeamName = m.TeamB };
                map[keyB] = rowB;
            }

            rowA.Played++;
            rowB.Played++;
            rowA.GoalsFor += a;
            rowA.GoalsAgainst += b;
            rowB.GoalsFor += b;
            rowB.GoalsAgainst += a;

            if (a > b)
            {
                rowA.Won++;
                rowA.Points += 3;
                rowB.Lost++;
            }
            else if (b > a)
            {
                rowB.Won++;
                rowB.Points += 3;
                rowA.Lost++;
            }
            else
            {
                rowA.Draw++;
                rowA.Points++;
                rowB.Draw++;
                rowB.Points++;
            }
        }

        foreach (var row in map.Values)
        {
            row.GoalDifference = row.GoalsFor - row.GoalsAgainst;
        }

        await db.GroupStandings.AddRangeAsync(map.Values);
        await db.SaveChangesAsync();
    }
}

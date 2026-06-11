using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Services;

public class KnockoutEngine(AppDbContext db)
{
    public async Task BuildRoundOf16Async()
    {
        var standings = await db.GroupStandings.ToListAsync();
        if (!standings.Any())
        {
            return;
        }

        var groupWinners = standings
            .GroupBy(x => x.GroupCode)
            .Select(g => g.OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalDifference).ThenByDescending(x => x.GoalsFor).First())
            .OrderBy(x => x.GroupCode)
            .ToList();

        var groupRunnersUp = standings
            .GroupBy(x => x.GroupCode)
            .Select(g => g.OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalDifference).ThenByDescending(x => x.GoalsFor).Skip(1).FirstOrDefault())
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.GroupCode)
            .ToList();

        var existing = await db.Matches.AnyAsync(x => x.Round == TournamentRound.RoundOf16);
        if (existing)
        {
            return;
        }

        var count = Math.Min(groupWinners.Count, groupRunnersUp.Count);
        for (var i = 0; i < count; i++)
        {
            var m = new Match
            {
                MatchTime = DateTime.UtcNow.AddDays(30 + i),
                TeamA = groupWinners[i].TeamName,
                TeamB = groupRunnersUp[count - 1 - i].TeamName,
                FavoriteTeam = groupWinners[i].TeamName,
                HandicapValue = 0,
                Round = TournamentRound.RoundOf16,
                GroupCode = "KO"
            };
            db.Matches.Add(m);
        }

        await db.SaveChangesAsync();
    }

    public async Task AdvanceWinnersAsync()
    {
        await BuildNextRoundAsync(TournamentRound.RoundOf16, TournamentRound.QuarterFinal, 40);
        await BuildNextRoundAsync(TournamentRound.QuarterFinal, TournamentRound.SemiFinal, 45);
        await BuildNextRoundAsync(TournamentRound.SemiFinal, TournamentRound.Final, 50);
        await BuildThirdPlaceAsync();
    }

    private async Task BuildNextRoundAsync(TournamentRound from, TournamentRound to, int dayOffset)
    {
        var current = await db.Matches.Where(x => x.Round == from && !string.IsNullOrWhiteSpace(x.Result)).OrderBy(x => x.Id).ToListAsync();
        if (!current.Any())
        {
            return;
        }

        var nextExists = await db.Matches.AnyAsync(x => x.Round == to);
        if (nextExists)
        {
            return;
        }

        var winners = new List<string>();
        foreach (var m in current)
        {
            if (!ResultParser.TryParseScore(m.Result, out var a, out var b))
            {
                continue;
            }

            winners.Add(a >= b ? m.TeamA : m.TeamB);
        }

        for (var i = 0; i + 1 < winners.Count; i += 2)
        {
            db.Matches.Add(new Match
            {
                MatchTime = DateTime.UtcNow.AddDays(dayOffset + i),
                TeamA = winners[i],
                TeamB = winners[i + 1],
                FavoriteTeam = winners[i],
                HandicapValue = 0,
                Round = to,
                GroupCode = "KO"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task BuildThirdPlaceAsync()
    {
        var exists = await db.Matches.AnyAsync(x => x.Round == TournamentRound.ThirdPlace);
        if (exists)
        {
            return;
        }

        var semis = await db.Matches.Where(x => x.Round == TournamentRound.SemiFinal && !string.IsNullOrWhiteSpace(x.Result)).ToListAsync();
        if (semis.Count < 2)
        {
            return;
        }

        var losers = new List<string>();
        foreach (var m in semis)
        {
            if (!ResultParser.TryParseScore(m.Result, out var a, out var b))
            {
                continue;
            }

            losers.Add(a >= b ? m.TeamB : m.TeamA);
        }

        if (losers.Count == 2)
        {
            db.Matches.Add(new Match
            {
                MatchTime = DateTime.UtcNow.AddDays(52),
                TeamA = losers[0],
                TeamB = losers[1],
                FavoriteTeam = losers[0],
                HandicapValue = 0,
                Round = TournamentRound.ThirdPlace,
                GroupCode = "KO"
            });
            await db.SaveChangesAsync();
        }
    }
}

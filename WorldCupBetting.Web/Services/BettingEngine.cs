using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Services;

public class BettingEngine(AppDbContext db, IClockService clock)
{
    private const int CorrectAmount = -1;  // Xanh: -1 điểm
    private const int WrongAmount = 2;     // Đỏ: +2 điểm

    public async Task RecalculateAllAsync()
    {
        var matches = await db.Matches
            .Where(x => !string.IsNullOrWhiteSpace(x.Result))
            .OrderBy(x => x.MatchTime)
            .ToListAsync();

        foreach (var match in matches)
        {
            await RecalculateMatchAsync(match.Id);
        }
    }

    public async Task RecalculateMatchAsync(int matchId)
    {
        var match = await db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (match is null || string.IsNullOrWhiteSpace(match.Result))
        {
            return;
        }

        match.Status = MatchStatus.Finished;

        var users = await db.Users.ToListAsync();
        var predictions = await db.Predictions.Where(x => x.MatchId == matchId).ToListAsync();
        var existing = await db.BetResults.Where(x => x.MatchId == matchId).ToListAsync();

        var result = match.Result;

        foreach (var user in users)
        {
            var prediction = predictions.FirstOrDefault(x => x.UserId == user.Id);
            var selected = prediction?.SelectedTeam;

            int amount = 0;
            string outcome = "Chưa có dự đoán";

            if (!string.IsNullOrWhiteSpace(selected))
            {
                bool isCorrect = string.Equals(selected, result, StringComparison.OrdinalIgnoreCase);
                amount = isCorrect ? CorrectAmount : WrongAmount;
                outcome = isCorrect ? "Đúng" : "Sai";
            }

            var row = existing.FirstOrDefault(x => x.UserId == user.Id);
            if (row is null)
            {
                db.BetResults.Add(new BetResult
                {
                    MatchId = matchId,
                    UserId = user.Id,
                    Amount = amount,
                    Outcome = outcome,
                    CalculatedAt = clock.VietnamNow()
                });
            }
            else
            {
                row.Amount = amount;
                row.Outcome = outcome;
                row.CalculatedAt = clock.VietnamNow();
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<int, int>> GetUserTotalsAsync()
    {
        return await db.BetResults
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(x => x.Key, x => x.Sum(v => v.Amount));
    }
}

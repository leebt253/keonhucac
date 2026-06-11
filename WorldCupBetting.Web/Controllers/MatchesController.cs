using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models.Enums;
using WorldCupBetting.Web.Services;
using WorldCupBetting.Web.ViewModels;

namespace WorldCupBetting.Web.Controllers;

[Authorize]
public class MatchesController(AppDbContext db, IClockService clock, BettingEngine bettingEngine) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string search = "", string group = "", string round = "")
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentUser = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        var mobileViewMode = string.Equals(currentUser?.MobileMatchViewMode, "full", StringComparison.OrdinalIgnoreCase)
            ? "full"
            : "compact";

        var users = await db.Users
            .Where(x => x.UserName != "admin")
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync();
        var query = db.Matches.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.TeamA.Contains(search) || x.TeamB.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            query = query.Where(x => x.GroupCode == group);
        }

        if (!string.IsNullOrWhiteSpace(round) && Enum.TryParse<TournamentRound>(round, out var roundValue))
        {
            query = query.Where(x => x.Round == roundValue);
        }

        var matches = await query.OrderBy(x => x.MatchTime).ToListAsync();
        var predictions = await db.Predictions.ToListAsync();
        var betResults = await db.BetResults.ToListAsync();

        foreach (var m in matches.Where(x => clock.IsLocked(x.MatchTime) && x.Status == MatchStatus.Upcoming))
        {
            m.Status = MatchStatus.Locked;
        }
        await db.SaveChangesAsync();

        var rows = matches.Select(m => new MatchRowViewModel
        {
            Match = m,
            PredictionsByUser = predictions.Where(p => p.MatchId == m.Id).ToDictionary(p => p.UserId, p => p.SelectedTeam),
            MoneyByUser = betResults.Where(b => b.MatchId == m.Id).ToDictionary(b => b.UserId, b => b.Amount),
            ResultsByUser = predictions
                .Where(p => p.MatchId == m.Id)
                .ToDictionary(
                    p => p.UserId,
                    p => string.IsNullOrWhiteSpace(m.Result) 
                        ? null 
                        : (bool?)(string.Equals(p.SelectedTeam, m.Result, StringComparison.OrdinalIgnoreCase))),
            IsLocked = clock.IsLocked(m.MatchTime) || m.Status == MatchStatus.Locked || m.Status == MatchStatus.Finished,
            MySelection = predictions.FirstOrDefault(p => p.MatchId == m.Id && p.UserId == userId)?.SelectedTeam,
            TeamAChoosers = string.Join(", ", predictions
                .Where(p => p.MatchId == m.Id && string.Equals(p.SelectedTeam, m.TeamA, StringComparison.OrdinalIgnoreCase))
                .Join(users, p => p.UserId, u => u.Id, (_, u) => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName)),
            TeamBChoosers = string.Join(", ", predictions
                .Where(p => p.MatchId == m.Id && string.Equals(p.SelectedTeam, m.TeamB, StringComparison.OrdinalIgnoreCase))
                .Join(users, p => p.UserId, u => u.Id, (_, u) => string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName))
        }).ToList();

        var totals = await bettingEngine.GetUserTotalsAsync();

        return View(new MatchIndexViewModel
        {
            Users = users,
            Rows = rows,
            TotalsByUser = totals,
            Search = search,
            Group = group,
            Round = round,
            MobileViewMode = mobileViewMode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMobileViewMode(string mode)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return NotFound();
        }

        user.MobileMatchViewMode = string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase) ? "full" : "compact";
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Predict(int matchId, string selectedTeam)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var match = await db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (match is null)
        {
            return NotFound();
        }

        if (clock.IsLocked(match.MatchTime) || match.Status == MatchStatus.Finished)
        {
            TempData["Error"] = "Trận đã khóa kèo";
            return RedirectToAction(nameof(Index));
        }

        // Allow empty selectedTeam to remove prediction (unselect)
        if (string.IsNullOrWhiteSpace(selectedTeam))
        {
            var existing = await db.Predictions.FirstOrDefaultAsync(x => x.UserId == userId && x.MatchId == matchId);
            if (existing is not null)
            {
                db.Predictions.Remove(existing);
                await db.SaveChangesAsync();
                TempData["Success"] = "Đã bỏ chọn dự đoán";
            }
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(selectedTeam, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(selectedTeam, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Lựa chọn không hợp lệ";
            return RedirectToAction(nameof(Index));
        }

        var existingPred = await db.Predictions.FirstOrDefaultAsync(x => x.UserId == userId && x.MatchId == matchId);
        if (existingPred is null)
        {
            db.Predictions.Add(new Models.Prediction
            {
                UserId = userId,
                MatchId = matchId,
                SelectedTeam = selectedTeam,
                PredictionTime = clock.VietnamNow()
            });
        }
        else
        {
            existingPred.SelectedTeam = selectedTeam;
            existingPred.PredictionTime = clock.VietnamNow();
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã lưu dự đoán";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOdds(int matchId, string favoriteTeam, decimal handicapValue)
    {
        var match = await db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (match is null)
        {
            return NotFound();
        }

        favoriteTeam = favoriteTeam?.Trim() ?? string.Empty;

        if (!string.Equals(favoriteTeam, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(favoriteTeam, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Đội chấp chỉ được chọn Đội A hoặc Đội B cùng hàng.";
            return RedirectToAction(nameof(Index));
        }

        match.FavoriteTeam = favoriteTeam;
        match.HandicapValue = handicapValue;
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật đội chấp và tỷ lệ chấp.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateResult(int matchId, string result)
    {
        var match = await db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (match is null)
        {
            return NotFound();
        }

        result = result?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(result) && 
            !string.Equals(result, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Kết quả phải là Đội A, Đội B hoặc để trống.";
            return RedirectToAction(nameof(Index));
        }

        match.Result = result;

        if (string.IsNullOrWhiteSpace(match.Result))
        {
            var existingResults = await db.BetResults.Where(x => x.MatchId == matchId).ToListAsync();
            if (existingResults.Count > 0)
            {
                db.BetResults.RemoveRange(existingResults);
            }

            match.Status = clock.IsLocked(match.MatchTime) ? MatchStatus.Locked : MatchStatus.Upcoming;
            await db.SaveChangesAsync();
        }
        else
        {
            await db.SaveChangesAsync();
            await bettingEngine.RecalculateMatchAsync(matchId);
        }

        TempData["Success"] = "Đã cập nhật kết quả trận đấu.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateKnockoutTeams(int matchId, string teamA, string teamB)
    {
        var match = await db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (match is null)
        {
            return NotFound();
        }

        var hadResult = !string.IsNullOrWhiteSpace(match.Result);

        var normalizedTeamA = teamA?.Trim();
        var normalizedTeamB = teamB?.Trim();

        // Empty input clears the team; non-empty input updates the team.
        match.TeamA = normalizedTeamA ?? string.Empty;
        match.TeamB = normalizedTeamB ?? string.Empty;

        // Keep favorite team valid: only TeamA/TeamB or empty.
        if (!string.Equals(match.FavoriteTeam, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(match.FavoriteTeam, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            match.FavoriteTeam = string.Empty;
        }

        // Keep result valid: only TeamA/TeamB or empty.
        if (!string.IsNullOrWhiteSpace(match.Result) &&
            !string.Equals(match.Result, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(match.Result, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            match.Result = string.Empty;
        }

        if (hadResult && string.IsNullOrWhiteSpace(match.Result))
        {
            var existingResults = await db.BetResults.Where(x => x.MatchId == matchId).ToListAsync();
            if (existingResults.Count > 0)
            {
                db.BetResults.RemoveRange(existingResults);
            }

            match.Status = clock.IsLocked(match.MatchTime) ? MatchStatus.Locked : MatchStatus.Upcoming;
        }

        await db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật đội cho vòng knockout.";
        return RedirectToAction(nameof(Index));
    }
}

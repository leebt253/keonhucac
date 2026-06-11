using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Services;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.ViewModels;

namespace WorldCupBetting.Web.Controllers;

[Authorize]
public class HomeController(AppDbContext db, BettingEngine bettingEngine, IClockService clock) : Controller
{
    public async Task<IActionResult> Index()
    {
        var matches = await db.Matches.ToListAsync();
        var totals = await bettingEngine.GetUserTotalsAsync();
        var users = await db.Users.ToDictionaryAsync(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName : x.DisplayName);

        var ordered = totals.OrderByDescending(x => x.Value).ToList();

        var vm = new DashboardViewModel
        {
            TotalMatches = matches.Count,
            FinishedMatches = matches.Count(x => !string.IsNullOrWhiteSpace(x.Result)),
            UpcomingMatches = matches.Count(x => x.MatchTime > clock.VietnamNow()),
            CurrentLeader = ordered.Count > 0 ? $"{users[ordered.First().Key]} ({ordered.First().Value})" : "-",
            HighestProfitUser = ordered.Count > 0 ? $"{users[ordered.First().Key]} ({ordered.First().Value})" : "-",
            LargestLossUser = ordered.Count > 0 ? $"{users[ordered.Last().Key]} ({ordered.Last().Value})" : "-"
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

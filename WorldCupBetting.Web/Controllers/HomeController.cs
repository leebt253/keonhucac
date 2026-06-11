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
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var hasFinishedMatch = await db.Matches.AnyAsync(x => !string.IsNullOrWhiteSpace(x.Result));

        var users = await db.Users
            .Where(x => x.UserName != "admin")
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync();

        var results = await db.BetResults.ToListAsync();

        var rows = users
            .Select(user =>
            {
                var userResults = results.Where(x => x.UserId == user.Id).ToList();
                var rankTitle = string.Empty;

                return new LeaderboardRowViewModel
                {
                    DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
                    CorrectPredictions = userResults.Count(x => x.Amount == -1),
                    WrongPredictions = userResults.Count(x => x.Amount == 2),
                    TotalAmount = userResults.Sum(x => x.Amount),
                    Title = rankTitle
                };
            })
            .OrderBy(x => x.TotalAmount)
            .ThenByDescending(x => x.CorrectPredictions)
            .ThenBy(x => x.WrongPredictions)
            .ThenBy(x => x.DisplayName)
            .ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Rank = i + 1;
            rows[i].Title = rows[i].Rank switch
            {
                1 => "Con nuôi của nhà cái",
                2 => "Gia Cát Dự",
                3 => "Kèo này dễ",
                >= 4 and <= 6 => "Tham gia cho vui",
                7 => "Kèo như cặc",
                _ => ""
            };
        }

        return View(new LeaderboardViewModel
        {
            Rows = rows,
            ShowTitles = hasFinishedMatch
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

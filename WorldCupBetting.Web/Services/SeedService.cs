using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Services;

public class SeedService(AppDbContext db, IClockService clock)
{
    public async Task SeedAsync()
    {
        await db.Database.MigrateAsync();

        var hasher = new PasswordHasher<AppUser>();
        var names = new[]
        {
            "Hai", "Hung", "Nam", "Quy", "Trung", "Truong", "Viet"
        };

        foreach (var name in names)
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == name);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = name,
                    IsAdmin = false,
                    CreatedAt = clock.VietnamNow()
                };
                user.PasswordHash = hasher.HashPassword(user, "123456");
                db.Users.Add(user);
            }

            user.IsAdmin = name == "Nam";
        }

        var admin = await db.Users.FirstOrDefaultAsync(x => x.UserName == "admin");
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = "admin",
                IsAdmin = true,
                CreatedAt = clock.VietnamNow()
            };
            admin.PasswordHash = hasher.HashPassword(admin, "Keonhucac");
            db.Users.Add(admin);
        }
        else
        {
            admin.IsAdmin = true;
            admin.PasswordHash = hasher.HashPassword(admin, "Keonhucac");
        }

        if (!await db.Teams.AnyAsync())
        {
            var teams = new[]
            {
                new Team { Name = "Brazil", GroupCode = "A", FlagUrl = "https://flagcdn.com/w80/br.png" },
                new Team { Name = "Nhật Bản", GroupCode = "A", FlagUrl = "https://flagcdn.com/w80/jp.png" },
                new Team { Name = "Anh", GroupCode = "B", FlagUrl = "https://flagcdn.com/w80/gb-eng.png" },
                new Team { Name = "Mỹ", GroupCode = "B", FlagUrl = "https://flagcdn.com/w80/us.png" },
                new Team { Name = "Pháp", GroupCode = "C", FlagUrl = "https://flagcdn.com/w80/fr.png" },
                new Team { Name = "Đức", GroupCode = "C", FlagUrl = "https://flagcdn.com/w80/de.png" },
                new Team { Name = "Argentina", GroupCode = "D", FlagUrl = "https://flagcdn.com/w80/ar.png" },
                new Team { Name = "Hà Lan", GroupCode = "D", FlagUrl = "https://flagcdn.com/w80/nl.png" }
            };
            await db.Teams.AddRangeAsync(teams);
        }

        if (!await db.Matches.AnyAsync())
        {
            var now = clock.VietnamNow().Date.AddHours(19);
            await db.Matches.AddRangeAsync(
                new Match
                {
                    MatchTime = now.AddDays(1),
                    TeamA = "Brazil",
                    TeamB = "Nhật Bản",
                    FavoriteTeam = "Brazil",
                    HandicapValue = 1.0m,
                    GroupCode = "A",
                    Round = TournamentRound.Group
                },
                new Match
                {
                    MatchTime = now.AddDays(1).AddHours(3),
                    TeamA = "Anh",
                    TeamB = "Mỹ",
                    FavoriteTeam = "Anh",
                    HandicapValue = 0.5m,
                    GroupCode = "B",
                    Round = TournamentRound.Group
                }
            );
        }

        await db.SaveChangesAsync();
    }
}

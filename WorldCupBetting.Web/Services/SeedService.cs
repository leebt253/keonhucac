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

        await DeduplicateGroupMatchesAsync();

        await db.SaveChangesAsync();
    }

    private async Task DeduplicateGroupMatchesAsync()
    {
        var groupMatches = await db.Matches
            .Where(x => x.Round == TournamentRound.Group)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (groupMatches.Count <= 1)
        {
            return;
        }

        var keepByKey = new Dictionary<string, Match>(StringComparer.OrdinalIgnoreCase);
        var duplicatePairs = new List<(Match Keep, Match Duplicate)>();

        foreach (var match in groupMatches)
        {
            var key = BuildPairKey(match.TeamA, match.TeamB);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!keepByKey.TryGetValue(key, out var keep))
            {
                keepByKey[key] = match;
                continue;
            }

            if (match.MatchTime < keep.MatchTime)
            {
                duplicatePairs.Add((match, keep));
                keepByKey[key] = match;
            }
            else
            {
                duplicatePairs.Add((keep, match));
            }
        }

        if (duplicatePairs.Count == 0)
        {
            return;
        }

        foreach (var pair in duplicatePairs)
        {
            var keep = pair.Keep;
            var duplicate = pair.Duplicate;

            if (string.IsNullOrWhiteSpace(keep.Result) && !string.IsNullOrWhiteSpace(duplicate.Result))
            {
                keep.Result = duplicate.Result;
            }

            if (string.IsNullOrWhiteSpace(keep.FavoriteTeam) && !string.IsNullOrWhiteSpace(duplicate.FavoriteTeam))
            {
                keep.FavoriteTeam = duplicate.FavoriteTeam;
            }

            if (keep.HandicapValue == 0 && duplicate.HandicapValue != 0)
            {
                keep.HandicapValue = duplicate.HandicapValue;
            }

            if (keep.MatchTime > duplicate.MatchTime)
            {
                keep.MatchTime = duplicate.MatchTime;
            }

            if (string.IsNullOrWhiteSpace(keep.GroupCode) && !string.IsNullOrWhiteSpace(duplicate.GroupCode))
            {
                keep.GroupCode = duplicate.GroupCode;
            }

            var duplicatePredictions = await db.Predictions.Where(x => x.MatchId == duplicate.Id).ToListAsync();
            foreach (var prediction in duplicatePredictions)
            {
                var existing = await db.Predictions
                    .FirstOrDefaultAsync(x => x.MatchId == keep.Id && x.UserId == prediction.UserId);

                if (existing is null)
                {
                    prediction.MatchId = keep.Id;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.SelectedTeam) && !string.IsNullOrWhiteSpace(prediction.SelectedTeam))
                    {
                        existing.SelectedTeam = prediction.SelectedTeam;
                    }

                    if (prediction.PredictionTime > existing.PredictionTime)
                    {
                        existing.PredictionTime = prediction.PredictionTime;
                    }

                    db.Predictions.Remove(prediction);
                }
            }

            var duplicateResults = await db.BetResults.Where(x => x.MatchId == duplicate.Id).ToListAsync();
            foreach (var result in duplicateResults)
            {
                var existing = await db.BetResults
                    .FirstOrDefaultAsync(x => x.MatchId == keep.Id && x.UserId == result.UserId);

                if (existing is null)
                {
                    result.MatchId = keep.Id;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.Outcome) && !string.IsNullOrWhiteSpace(result.Outcome))
                    {
                        existing.Outcome = result.Outcome;
                    }

                    if (existing.Amount == 0 && result.Amount != 0)
                    {
                        existing.Amount = result.Amount;
                    }

                    if (result.CalculatedAt > existing.CalculatedAt)
                    {
                        existing.CalculatedAt = result.CalculatedAt;
                    }

                    db.BetResults.Remove(result);
                }
            }

            var parentRefs = await db.Matches
                .Where(x => x.ParentMatchAId == duplicate.Id || x.ParentMatchBId == duplicate.Id)
                .ToListAsync();

            foreach (var match in parentRefs)
            {
                if (match.ParentMatchAId == duplicate.Id)
                {
                    match.ParentMatchAId = keep.Id;
                }

                if (match.ParentMatchBId == duplicate.Id)
                {
                    match.ParentMatchBId = keep.Id;
                }
            }

            db.Matches.Remove(duplicate);
        }
    }

    private static string BuildPairKey(string teamA, string teamB)
    {
        var a = teamA?.Trim() ?? string.Empty;
        var b = teamB?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return string.Empty;
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{a}||{b}"
            : $"{b}||{a}";
    }
}

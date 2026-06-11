using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Services;

namespace WorldCupBetting.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(
    AppDbContext db,
    IClockService clock) : Controller
{
    private async Task EnsureNamAdminAsync()
    {
        var nam = await db.Users.FirstOrDefaultAsync(x => x.UserName == "Nam");
        var hasher = new PasswordHasher<AppUser>();

        if (nam is null)
        {
            nam = new AppUser
            {
                UserName = "Nam",
                DisplayName = "Nam",
                IsAdmin = true,
                MobileMatchViewMode = "compact",
                CreatedAt = clock.VietnamNow()
            };
            nam.PasswordHash = hasher.HashPassword(nam, "123456");
            db.Users.Add(nam);
            await db.SaveChangesAsync();
            return;
        }

        if (!nam.IsAdmin)
        {
            nam.IsAdmin = true;
        }

        if (string.IsNullOrWhiteSpace(nam.DisplayName))
        {
            nam.DisplayName = "Nam";
        }

        if (string.IsNullOrWhiteSpace(nam.MobileMatchViewMode))
        {
            nam.MobileMatchViewMode = "compact";
        }

        await db.SaveChangesAsync();
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        await EnsureNamAdminAsync();

        var users = await db.Users
            .Where(x => x.UserName != "admin")
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync();
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string userName, string displayName)
    {
        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var normalizedDisplayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            TempData["Error"] = "Tên người chơi không được để trống.";
            return RedirectToAction(nameof(Users));
        }

        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            normalizedDisplayName = normalizedUserName;
        }

        var exists = await db.Users.AnyAsync(x => x.UserName.ToLower() == normalizedUserName.ToLower());
        if (exists)
        {
            TempData["Error"] = "Tên người chơi đã tồn tại.";
            return RedirectToAction(nameof(Users));
        }

        var newUser = new AppUser
        {
            UserName = normalizedUserName,
            DisplayName = normalizedDisplayName,
            IsAdmin = false,
            MobileMatchViewMode = "compact",
            CreatedAt = clock.VietnamNow()
        };

        var hasher = new PasswordHasher<AppUser>();
        newUser.PasswordHash = hasher.HashPassword(newUser, "123456");
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm người chơi {newUser.DisplayName}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplayName(int id, string displayName)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var normalizedDisplayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            TempData["Error"] = "Tên hiển thị không được để trống.";
            return RedirectToAction(nameof(Users));
        }

        user.DisplayName = normalizedDisplayName;
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật tên hiển thị cho {user.UserName}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, "123456");
        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã reset mật khẩu cho {user.DisplayName}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsAdmin)
        {
            TempData["Error"] = "Không thể xóa tài khoản quản trị.";
            return RedirectToAction(nameof(Users));
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa người chơi {user.DisplayName}.";
        return RedirectToAction(nameof(Users));
    }
}

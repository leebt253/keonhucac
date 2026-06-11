using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.ViewModels;

namespace WorldCupBetting.Web.Controllers;

public class AccountController(AppDbContext db) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Matches");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == vm.UserName);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
            return View(vm);
        }

        var hasher = new PasswordHasher<AppUser>();
        var ok = hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password);
        if (ok == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
            return View(vm);
        }

        // Keep Nam as admin if the role was changed unintentionally.
        if (string.Equals(user.UserName, "Nam", StringComparison.Ordinal) && !user.IsAdmin)
        {
            user.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName),
            new("UserName", user.UserName),
            new("IsAdmin", user.IsAdmin ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = vm.RememberMe,
            ExpiresUtc = vm.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authProperties);

        return RedirectToAction("Index", "Matches");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FirstAsync(x => x.Id == userId);
        var hasher = new PasswordHasher<AppUser>();
        var ok = hasher.VerifyHashedPassword(user, user.PasswordHash, vm.CurrentPassword);
        if (ok == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(ChangePasswordViewModel.CurrentPassword), "Mật khẩu hiện tại chưa đúng");
            return View(vm);
        }

        user.PasswordHash = hasher.HashPassword(user, vm.NewPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đổi mật khẩu thành công";
        return RedirectToAction("Index", "Matches");
    }
}

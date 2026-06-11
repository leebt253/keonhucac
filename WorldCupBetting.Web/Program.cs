using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "worldcup.auth";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("IsAdmin", "true"));
});

builder.Services.AddScoped<IClockService, VietnamClockService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<BettingEngine>();
builder.Services.AddScoped<StandingsEngine>();
builder.Services.AddScoped<KnockoutEngine>();
builder.Services.AddScoped<ExcelScheduleImporter>();

var app = builder.Build();

var culture = new CultureInfo("vi-VN");
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

using (var scope = app.Services.CreateScope())
{
    BootstrapDatabaseFromBundledFile(builder.Configuration.GetConnectionString("DefaultConnection"), app.Environment.ContentRootPath, app.Logger);

    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.SeedAsync();
}

static void BootstrapDatabaseFromBundledFile(string? connectionString, string contentRootPath, ILogger logger)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    try
    {
        var csb = new SqliteConnectionStringBuilder(connectionString);
        var targetDbPath = csb.DataSource;
        if (string.IsNullOrWhiteSpace(targetDbPath) || !Path.IsPathRooted(targetDbPath))
        {
            return;
        }

        var bundledDbPath = Path.Combine(contentRootPath, "worldcup.db");
        if (!File.Exists(bundledDbPath))
        {
            logger.LogWarning("Bundled database file not found at {Path}.", bundledDbPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetDbPath)!);

        if (!File.Exists(targetDbPath))
        {
            File.Copy(bundledDbPath, targetDbPath, overwrite: false);
            logger.LogInformation("Initialized database from bundled file to {Path}.", targetDbPath);
            return;
        }

        var targetMatches = GetMatchCount(targetDbPath);
        var bundledMatches = GetMatchCount(bundledDbPath);
        if (targetMatches >= 0 && targetMatches <= 2 && bundledMatches > targetMatches)
        {
            File.Copy(targetDbPath, targetDbPath + ".bak", overwrite: true);
            File.Copy(bundledDbPath, targetDbPath, overwrite: true);
            logger.LogInformation("Replaced sparse database at {Path} with bundled data (target={TargetMatches}, bundled={BundledMatches}).", targetDbPath, targetMatches, bundledMatches);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to bootstrap database from bundled file.");
    }
}

static int GetMatchCount(string dbPath)
{
    try
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Matches";
        var scalar = command.ExecuteScalar();

        return scalar is null || scalar == DBNull.Value
            ? -1
            : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }
    catch
    {
        return -1;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Matches}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

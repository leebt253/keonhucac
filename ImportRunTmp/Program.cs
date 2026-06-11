using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Data;
using WorldCupBetting.Web.Services;

var dbPath = "c:\\Users\\Lee\\Desktop\\WC\\WorldCupBetting.Web\\worldcup.db";
var schedulePath = "c:\\Users\\Lee\\Desktop\\WC\\Schedule.xlsx";

var options = new DbContextOptionsBuilder<AppDbContext>()
	.UseSqlite($"Data Source={dbPath}")
	.Options;

await using var db = new AppDbContext(options);
var importer = new ExcelScheduleImporter(db);
var standings = new StandingsEngine(db);

var count = await importer.ImportAsync(schedulePath);
await standings.RecalculateAsync();

Console.WriteLine($"Imported matches: {count}");

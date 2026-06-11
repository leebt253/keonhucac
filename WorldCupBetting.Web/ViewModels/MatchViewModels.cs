using WorldCupBetting.Web.Models;

namespace WorldCupBetting.Web.ViewModels;

public class MatchRowViewModel
{
    public Match Match { get; set; } = null!;
    public Dictionary<int, string> PredictionsByUser { get; set; } = new();
    public Dictionary<int, int> MoneyByUser { get; set; } = new();
    public Dictionary<int, bool?> ResultsByUser { get; set; } = new(); // true = correct (green), false = wrong (red), null = no prediction
    public bool IsLocked { get; set; }
    public string? MySelection { get; set; }
    public string TeamAChoosers { get; set; } = string.Empty;
    public string TeamBChoosers { get; set; } = string.Empty;
}

public class MatchIndexViewModel
{
    public List<AppUser> Users { get; set; } = new();
    public List<MatchRowViewModel> Rows { get; set; } = new();
    public Dictionary<int, int> TotalsByUser { get; set; } = new();
    public string Search { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Round { get; set; } = string.Empty;
}

public class PredictionOverviewViewModel
{
    public List<AppUser> Users { get; set; } = new();
    public List<MatchRowViewModel> Rows { get; set; } = new();
}

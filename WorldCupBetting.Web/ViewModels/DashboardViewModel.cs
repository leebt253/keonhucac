namespace WorldCupBetting.Web.ViewModels;

public class LeaderboardRowViewModel
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int CorrectPredictions { get; set; }
    public int WrongPredictions { get; set; }
    public int TotalAmount { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class LeaderboardViewModel
{
    public List<LeaderboardRowViewModel> Rows { get; set; } = new();
    public bool ShowTitles { get; set; }
}

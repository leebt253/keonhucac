namespace WorldCupBetting.Web.ViewModels;

public class DashboardViewModel
{
    public int TotalMatches { get; set; }
    public int UpcomingMatches { get; set; }
    public int FinishedMatches { get; set; }
    public string CurrentLeader { get; set; } = "-";
    public string HighestProfitUser { get; set; } = "-";
    public string LargestLossUser { get; set; } = "-";
}

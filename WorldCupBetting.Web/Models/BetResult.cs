namespace WorldCupBetting.Web.Models;

public class BetResult
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public int Amount { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public DateTime CalculatedAt { get; set; }
}

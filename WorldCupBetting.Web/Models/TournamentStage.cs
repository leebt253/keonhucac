using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Models;

public class TournamentStage
{
    public int Id { get; set; }

    public TournamentRound Round { get; set; }

    public int MatchId { get; set; }

    public string TeamA { get; set; } = string.Empty;

    public string TeamB { get; set; } = string.Empty;

    public string Winner { get; set; } = string.Empty;
}

using WorldCupBetting.Web.Models;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.ViewModels;

public class StandingsPageViewModel
{
    public List<IGrouping<string, GroupStanding>> Groups { get; set; } = new();
}

public class KnockoutViewModel
{
    public Dictionary<TournamentRound, List<Match>> RoundMatches { get; set; } = new();
}

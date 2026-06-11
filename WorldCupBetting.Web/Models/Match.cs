using System.ComponentModel.DataAnnotations;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.Models;

public class Match
{
    public int Id { get; set; }

    public DateTime MatchTime { get; set; }

    [Required, MaxLength(100)]
    public string TeamA { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TeamB { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FavoriteTeam { get; set; } = string.Empty;

    public decimal HandicapValue { get; set; }

    [MaxLength(20)]
    public string Result { get; set; } = string.Empty;

    public MatchStatus Status { get; set; }

    public TournamentRound Round { get; set; } = TournamentRound.Group;

    [MaxLength(10)]
    public string GroupCode { get; set; } = string.Empty;

    public int? ParentMatchAId { get; set; }

    public int? ParentMatchBId { get; set; }

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<BetResult> BetResults { get; set; } = new List<BetResult>();
}

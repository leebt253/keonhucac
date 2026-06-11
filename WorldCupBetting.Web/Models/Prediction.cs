using System.ComponentModel.DataAnnotations;

namespace WorldCupBetting.Web.Models;

public class Prediction
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    [Required, MaxLength(100)]
    public string SelectedTeam { get; set; } = string.Empty;

    public DateTime PredictionTime { get; set; }
}

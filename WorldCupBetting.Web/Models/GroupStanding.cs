using System.ComponentModel.DataAnnotations;

namespace WorldCupBetting.Web.Models;

public class GroupStanding
{
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string GroupCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TeamName { get; set; } = string.Empty;

    public int Played { get; set; }
    public int Won { get; set; }
    public int Draw { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
}

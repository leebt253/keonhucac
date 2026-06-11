using System.ComponentModel.DataAnnotations;

namespace WorldCupBetting.Web.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<BetResult> BetResults { get; set; } = new List<BetResult>();
}

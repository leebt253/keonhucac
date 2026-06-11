using System.ComponentModel.DataAnnotations;

namespace WorldCupBetting.Web.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    [Required, MaxLength(20)]
    public string MobileMatchViewMode { get; set; } = "compact";

    public DateTime CreatedAt { get; set; }

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<BetResult> BetResults { get; set; } = new List<BetResult>();
}

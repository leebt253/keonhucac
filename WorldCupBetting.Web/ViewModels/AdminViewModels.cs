using System.ComponentModel.DataAnnotations;
using WorldCupBetting.Web.Models.Enums;

namespace WorldCupBetting.Web.ViewModels;

public class MatchInputViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời gian")]
    [Display(Name = "Thời gian")]
    public DateTime MatchTime { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đội A")]
    [Display(Name = "Đội A")]
    public string TeamA { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đội B")]
    [Display(Name = "Đội B")]
    public string TeamB { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đội cửa trên")]
    [Display(Name = "Đội cửa trên")]
    public string FavoriteTeam { get; set; } = string.Empty;

    [Display(Name = "Tỷ lệ chấp")]
    public decimal HandicapValue { get; set; }

    [Display(Name = "Kết quả")]
    public string Result { get; set; } = string.Empty;

    [Display(Name = "Bảng")]
    public string GroupCode { get; set; } = string.Empty;

    [Display(Name = "Vòng")]
    public TournamentRound Round { get; set; }
}

public class TeamInputViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đội")]
    [Display(Name = "Tên đội")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Bảng")]
    public string GroupCode { get; set; } = string.Empty;

    [Display(Name = "URL cờ")]
    public string FlagUrl { get; set; } = string.Empty;
}

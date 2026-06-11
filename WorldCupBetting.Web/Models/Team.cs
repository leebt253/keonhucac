using System.ComponentModel.DataAnnotations;

namespace WorldCupBetting.Web.Models;

public class Team
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string GroupCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FlagUrl { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class UserReportInputViewModel
{
    [Required] public string TargetUserId { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = string.Empty;
    public Guid? TradeId { get; set; }

    [Required, StringLength(120), Display(Name = "Razlog")]
    public string Reason { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "Podrobnosti")]
    public string? Details { get; set; }
}

public sealed class BlockedUsersViewModel
{
    public List<BlockedUserRowViewModel> Rows { get; set; } = [];
}

public sealed class BlockedUserRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? City { get; set; }
    public DateTime CreatedUtc { get; set; }
}

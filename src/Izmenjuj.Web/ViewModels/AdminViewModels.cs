using Izmenjuj.Web.Domain;
using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class AdminDashboardViewModel
{
    public int Users { get; set; }
    public int Collections { get; set; }
    public int Items { get; set; }
    public int ActiveTrades { get; set; }
    public int OpenReports { get; set; }
    public int SuspendedUsers { get; set; }
}

public sealed class AdminCollectionsViewModel
{
    public string? Query { get; set; }
    public bool? Published { get; set; }
    public List<AdminCollectionRowViewModel> Rows { get; set; } = [];
}

public sealed class AdminCollectionRowViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public CollectionType Type { get; set; }
    public bool IsPublished { get; set; }
    public int ItemCount { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string OwnerName { get; set; } = string.Empty;
}

public sealed class AdminUsersViewModel
{
    public string? Query { get; set; }
    public List<AdminUserRowViewModel> Rows { get; set; } = [];
}

public sealed class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? City { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsAdmin { get; set; }
    public int OpenReports { get; set; }
}

public sealed class AdminReportsViewModel
{
    public UserReportStatus? Status { get; set; }
    public List<AdminReportRowViewModel> Rows { get; set; } = [];
}

public sealed class AdminReportRowViewModel
{
    public Guid Id { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public Guid? TradeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public UserReportStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? ResolutionNote { get; set; }
}

public sealed class AdminTradesViewModel
{
    public TradeStatus? Status { get; set; }
    public List<AdminTradeSummaryViewModel> Rows { get; set; } = [];
}

public sealed class AdminTradeSummaryViewModel
{
    public Guid Id { get; set; }
    public string ProposerName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public TradeStatus Status { get; set; }
    public int LineQuantity { get; set; }
    public int MessageCount { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class AdminTradeInspectViewModel
{
    public Guid Id { get; set; }
    public string ProposerName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public TradeStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<AdminTradeLineViewModel> Lines { get; set; } = [];
    public List<AdminTradeMessageViewModel> Messages { get; set; } = [];
    public List<AdminTradeEventViewModel> Events { get; set; } = [];
}

public sealed class AdminTradeLineViewModel
{
    public string FromUserName { get; set; } = string.Empty;
    public string ItemLabel { get; set; } = string.Empty;
    public string CollectionTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class AdminTradeMessageViewModel
{
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentUtc { get; set; }
}

public sealed class AdminTradeEventViewModel
{
    public string ActorName { get; set; } = string.Empty;
    public TradeEventType Type { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class AdminSuspendInputViewModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    public bool Suspended { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

public sealed class AdminResolveReportViewModel
{
    public Guid Id { get; set; }
    public UserReportStatus Status { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

public sealed class AdminBulkItemsViewModel
{
    public Guid CollectionId { get; set; }
    public string CollectionTitle { get; set; } = string.Empty;
    [Required, Display(Name = "Predmeti")]
    public string Data { get; set; } = string.Empty;
    public string? ResultMessage { get; set; }
}

public sealed class AdminMergeItemsViewModel
{
    public Guid CollectionId { get; set; }
    public string CollectionTitle { get; set; } = string.Empty;
    public Guid SourceItemId { get; set; }
    public Guid TargetItemId { get; set; }
    public List<AdminItemOptionViewModel> Items { get; set; } = [];
}

public sealed class AdminItemOptionViewModel
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

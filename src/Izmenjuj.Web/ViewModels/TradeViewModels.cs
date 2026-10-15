using Izmenjuj.Web.Domain;
using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class TradeEditViewModel
{
    public Guid? TradeId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherDisplayName { get; set; } = string.Empty;
    public bool IsCounter { get; set; }
    public List<TradeOfferItemViewModel> MyItems { get; set; } = [];
    public List<TradeOfferItemViewModel> TheirItems { get; set; } = [];

    [StringLength(2000)]
    [Display(Name = "Sporočilo ob predlogu")]
    public string? Message { get; set; }
}

public sealed class TradeOfferItemViewModel
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CollectionTitle { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? PrimaryImagePath { get; set; }
    public int MaxQuantity { get; set; }
    public bool Selected { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;
}

public sealed class TradeIndexViewModel
{
    public List<TradeSummaryViewModel> Active { get; set; } = [];
    public List<TradeSummaryViewModel> History { get; set; } = [];
}

public sealed class TradeSummaryViewModel
{
    public Guid Id { get; set; }
    public string OtherDisplayName { get; set; } = string.Empty;
    public string? OtherCity { get; set; }
    public TradeStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int YouGiveCount { get; set; }
    public int YouReceiveCount { get; set; }
    public string StatusText => Status switch
    {
        TradeStatus.Proposed => "Predlog",
        TradeStatus.Accepted => "Sprejeto – čaka zaključek",
        TradeStatus.Completed => "Zaključeno",
        TradeStatus.Rejected => "Zavrnjeno",
        TradeStatus.Cancelled => "Preklicano",
        _ => Status.ToString()
    };
}

public sealed class TradeDetailsViewModel
{
    public Guid Id { get; set; }
    public TradeStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherDisplayName { get; set; } = string.Empty;
    public string? OtherCity { get; set; }
    public bool IsProposer { get; set; }
    public string? LastOfferActorUserId { get; set; }
    public List<TradeLineViewModel> YouGive { get; set; } = [];
    public List<TradeLineViewModel> YouReceive { get; set; } = [];
    public List<TradeMessageViewModel> Messages { get; set; } = [];
    public List<TradeEventViewModel> Events { get; set; } = [];
    public List<TradeRatingViewModel> Ratings { get; set; } = [];
    public bool YouConfirmedCompletion { get; set; }
    public bool OtherConfirmedCompletion { get; set; }
    public bool YouRated { get; set; }

    public bool CanAccept => Status == TradeStatus.Proposed && LastOfferActorUserId != CurrentUserId;
    public bool CanCounter => Status == TradeStatus.Proposed && LastOfferActorUserId != CurrentUserId;
    public bool CanCancel => Status == TradeStatus.Proposed && LastOfferActorUserId == CurrentUserId;
    public bool CanReject => Status == TradeStatus.Proposed && LastOfferActorUserId != CurrentUserId;
    public bool CanConfirmCompletion => Status == TradeStatus.Accepted && !YouConfirmedCompletion;
    public bool CanRate => Status == TradeStatus.Completed && !YouRated;
}

public sealed class TradeLineViewModel
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CollectionTitle { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? PrimaryImagePath { get; set; }
    public int Quantity { get; set; }
}

public sealed class TradeMessageViewModel
{
    public Guid Id { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentUtc { get; set; }
}

public sealed class TradeEventViewModel
{
    public TradeEventType Type { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }

    public string Text => Type switch
    {
        TradeEventType.Proposed => "je poslal predlog menjave",
        TradeEventType.Countered => "je poslal protipredlog",
        TradeEventType.Accepted => "je sprejel predlog",
        TradeEventType.Rejected => "je zavrnil predlog",
        TradeEventType.Cancelled => "je preklical predlog",
        TradeEventType.CompletionConfirmed => "je potrdil zaključek menjave",
        TradeEventType.Completed => "menjava je bila zaključena in evidence posodobljene",
        _ => Type.ToString()
    };
}

public sealed class TradeRatingViewModel
{
    public string FromDisplayName { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class TradeMessageInputViewModel
{
    public Guid TradeId { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}

public sealed class TradeRatingInputViewModel
{
    public Guid TradeId { get; set; }
    [Range(1, 5)]
    public int Stars { get; set; } = 5;
    [StringLength(1000)]
    public string? Comment { get; set; }
}

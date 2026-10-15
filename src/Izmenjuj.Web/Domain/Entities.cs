using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.Domain;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(80)] public string DisplayName { get; set; } = string.Empty;
    [MaxLength(80)] public string? City { get; set; }
    public bool TradesByMail { get; set; } = true;
    public bool TradesInPerson { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public enum CollectionType { Stickers, Cards, Figurines, Other }
public enum InventoryState { Have, Wanted, ForTrade }
public enum TradeStatus { Draft, Proposed, Accepted, Completed, Rejected, Cancelled }
public enum TradeEventType { Proposed, Countered, Accepted, Rejected, Cancelled, CompletionConfirmed, Completed }

public sealed class Collection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public required string Title { get; set; }
    [MaxLength(160)] public string? Subtitle { get; set; }
    [MaxLength(4000)] public string? Description { get; set; }
    public int? Year { get; set; }
    [MaxLength(160)] public string? Manufacturer { get; set; }
    [MaxLength(80)] public string? Country { get; set; }
    public CollectionType Type { get; set; }
    public bool IsPublished { get; set; } = true;
    [MaxLength(500)] public string? CoverImagePath { get; set; }
    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<CollectionImage> Images { get; set; } = [];
    public List<CollectibleItem> Items { get; set; } = [];
}

public sealed class CollectionImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    [MaxLength(500)] public required string ImagePath { get; set; }
    [MaxLength(120)] public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CollectibleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    [MaxLength(160)] public required string Name { get; set; }
    [MaxLength(80)] public string? OfficialCode { get; set; }
    [MaxLength(80)] public required string InternalCode { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(120)] public string? Variant { get; set; }
    [MaxLength(500)] public string? PrimaryImagePath { get; set; }
    public List<ItemImage> Images { get; set; } = [];
}

public sealed class ItemImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectibleItemId { get; set; }
    public CollectibleItem Item { get; set; } = null!;
    [MaxLength(500)] public required string ImagePath { get; set; }
    [MaxLength(120)] public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UserInventory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid CollectibleItemId { get; set; }
    public CollectibleItem Item { get; set; } = null!;
    public InventoryState State { get; set; }
    public int Quantity { get; set; } = 1;
    [MaxLength(500)] public string? Notes { get; set; }
}

public sealed class Trade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ProposerUserId { get; set; }
    public required string RecipientUserId { get; set; }
    public TradeStatus Status { get; set; } = TradeStatus.Draft;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<TradeLine> Lines { get; set; } = [];
    public List<TradeMessage> Messages { get; set; } = [];
    public List<TradeEvent> Events { get; set; } = [];
    public List<TradeRating> Ratings { get; set; } = [];
}

public sealed class TradeLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
    public Guid CollectibleItemId { get; set; }
    public CollectibleItem Item { get; set; } = null!;
    public required string FromUserId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class TradeMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
    [MaxLength(450)] public required string SenderUserId { get; set; }
    [MaxLength(2000)] public required string Body { get; set; }
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TradeEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
    [MaxLength(450)] public required string ActorUserId { get; set; }
    public TradeEventType Type { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TradeRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
    [MaxLength(450)] public required string FromUserId { get; set; }
    [MaxLength(450)] public required string ToUserId { get; set; }
    [Range(1, 5)] public int Stars { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

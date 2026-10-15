using Izmenjuj.Web.Domain;

namespace Izmenjuj.Web.ViewModels;

public enum TradeModeFilter
{
    Any,
    Mail,
    InPerson
}

public sealed class MatchIndexViewModel
{
    public Guid? CollectionId { get; set; }
    public string? City { get; set; }
    public TradeModeFilter TradeMode { get; set; } = TradeModeFilter.Any;
    public List<MatchCollectionOptionViewModel> Collections { get; set; } = [];
    public List<UserMatchViewModel> Matches { get; set; } = [];
    public int MyWantedCount { get; set; }
    public int MyForTradeCount { get; set; }
}

public sealed class MatchCollectionOptionViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public sealed class UserMatchViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool TradesByMail { get; set; }
    public bool TradesInPerson { get; set; }
    public bool SameCity { get; set; }
    public int Score { get; set; }
    public int BalancedTradeCount { get; set; }
    public List<MatchItemViewModel> YouGive { get; set; } = [];
    public List<MatchItemViewModel> YouReceive { get; set; } = [];
    public List<MatchItemViewModel> SuggestedGive => YouGive.Take(BalancedTradeCount).ToList();
    public List<MatchItemViewModel> SuggestedReceive => YouReceive.Take(BalancedTradeCount).ToList();
}

public sealed class MatchItemViewModel
{
    public Guid ItemId { get; set; }
    public Guid CollectionId { get; set; }
    public string CollectionTitle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OfficialCode { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? PrimaryImagePath { get; set; }
}
